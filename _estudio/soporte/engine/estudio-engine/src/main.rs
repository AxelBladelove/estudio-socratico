use anyhow::{anyhow, Context, Result};
use chrono::{SecondsFormat, Utc};
use regex::Regex;
use reqwest::Client;
use rusqlite::{params, Connection};
use serde::{Deserialize, Serialize};
use serde_json::{json, Map, Value};
use std::collections::HashMap;
use std::env;
use std::ffi::OsString;
use std::fs;
use std::io::{self, BufRead, Write};
use std::path::{Path, PathBuf};
use std::process::Stdio;
use tokio::process::Command;
use tokio::time::{sleep, Duration};

#[derive(Clone)]
struct Engine {
    repo_root: PathBuf,
    assets_root: PathBuf,
    http: Client,
}

#[derive(Debug, Deserialize)]
struct RpcRequest {
    id: Value,
    method: String,
    #[serde(default)]
    params: Value,
}

#[derive(Debug, Serialize)]
struct RpcResponse {
    id: Value,
    ok: bool,
    #[serde(skip_serializing_if = "Option::is_none")]
    result: Option<Value>,
    #[serde(skip_serializing_if = "Option::is_none")]
    error: Option<String>,
}

#[derive(Debug, Clone)]
struct LocalExercise {
    folder: PathBuf,
    meta: Value,
}

#[tokio::main]
async fn main() -> Result<()> {
    let args: Vec<String> = env::args().skip(1).collect();
    if args.first().map(String::as_str) == Some("daemon") {
        let params = parse_cli_params(&args[1..]);
        let root = resolve_repo_root(
            params
                .get("repo-root")
                .or_else(|| params.get("reporoot"))
                .map(String::as_str),
        )?;
        let assets_root = resolve_assets_root(
            params
                .get("assets-root")
                .or_else(|| params.get("assetsroot"))
                .map(String::as_str),
            &root,
        )?;
        return daemon(root, assets_root).await;
    }

    if let Err(error) = direct_cli(args).await {
        eprintln!("{error}");
        std::process::exit(1);
    }
    Ok(())
}

async fn daemon(repo_root: PathBuf, assets_root: PathBuf) -> Result<()> {
    let engine = Engine::new(repo_root, assets_root)?;
    let stdin = io::stdin();
    let mut stdout = io::stdout();
    for line in stdin.lock().lines() {
        let line = line?;
        if line.trim().is_empty() {
            continue;
        }

        let response = match serde_json::from_str::<RpcRequest>(&line) {
            Ok(request) => {
                let id = request.id.clone();
                match engine.handle(&request.method, &request.params).await {
                    Ok(result) => RpcResponse {
                        id,
                        ok: true,
                        result: Some(result),
                        error: None,
                    },
                    Err(error) => RpcResponse {
                        id,
                        ok: false,
                        result: None,
                        error: Some(error.to_string()),
                    },
                }
            }
            Err(error) => RpcResponse {
                id: Value::Null,
                ok: false,
                result: None,
                error: Some(format!("JSON invalido: {error}")),
            },
        };
        writeln!(stdout, "{}", serde_json::to_string(&response)?)?;
        stdout.flush()?;
    }
    Ok(())
}

async fn direct_cli(args: Vec<String>) -> Result<()> {
    let (method, params) = parse_direct_invocation(args);
    let root_arg = string_param(&params, "repoRoot")
        .or_else(|| string_param(&params, "repo-root"))
        .or_else(|| string_param(&params, "reporoot"));
    let root = resolve_repo_root(root_arg.as_deref())?;
    let assets_arg = string_param(&params, "assetsRoot")
        .or_else(|| string_param(&params, "assets-root"))
        .or_else(|| string_param(&params, "assetsroot"));
    let assets_root = resolve_assets_root(assets_arg.as_deref(), &root)?;
    let engine = Engine::new(root, assets_root)?;

    match method.as_str() {
        "detect" => {
            let result = engine.handle("detect", &params).await?;
            if bool_value(&result, "isExercism") {
                println!("IS_EXERCISM=1");
                if let Some(root) = string_param(&result, "exerciseRoot") {
                    println!("EXERCISM_ROOT={root}");
                }
            } else {
                println!("IS_EXERCISM=0");
                if bool_value(&result, "isEstudioValidate") {
                    println!("IS_ESTUDIO_VALIDATE=1");
                    if let Some(root) = string_param(&result, "exerciseRoot") {
                        println!("ESTUDIO_VALIDATE_ROOT={root}");
                    }
                }
            }
        }
        "test" | "validate" => {
            let result = engine.handle(&method, &params).await?;
            for line in result
                .get("output")
                .and_then(Value::as_array)
                .into_iter()
                .flatten()
            {
                if let Some(text) = line.as_str() {
                    println!("{text}");
                }
            }
            std::process::exit(int_value(&result, "exitCode").unwrap_or(1));
        }
        _ => {
            let result = engine.handle(&method, &params).await?;
            println!("{}", serde_json::to_string(&result)?);
        }
    }

    Ok(())
}

impl Engine {
    fn new(repo_root: PathBuf, assets_root: PathBuf) -> Result<Self> {
        Ok(Self {
            repo_root,
            assets_root,
            http: Client::builder().timeout(Duration::from_secs(90)).build()?,
        })
    }

    async fn handle(&self, method: &str, params: &Value) -> Result<Value> {
        match method {
            "status" => self.status().await,
            "catalog" => self.catalog().await,
            "import" => self.import(params).await,
            "mark" => self.mark(params).await,
            "test" => self.test(params).await,
            "validate" => self.validate(params).await,
            "test-window" => self.window_command(params, "test").await,
            "validate-window" => self.window_command(params, "validate").await,
            "reveal-tests" => self.reveal_tests(params).await,
            "submit" => self.submit(params).await,
            "detect" => self.detect(params).await,
            "fundamentals.catalog" => self.fundamentals_catalog(),
            "fundamentals.route" => self.fundamentals_route(),
            "fundamentals.categories" => self.fundamentals_categories(),
            "fundamentals.quizzes" => self.fundamentals_quizzes(),
            "fundamentals.projects" => self.fundamentals_projects(),
            "fundamentals.progress" => self.fundamentals_progress(),
            "fundamentals.recommendations" => self.fundamentals_recommendations(),
            "fundamentals.unlocks" => self.fundamentals_unlocks(),
            other => Err(anyhow!("Metodo desconocido: {other}")),
        }
    }

    async fn status(&self) -> Result<Value> {
        let cli = exercism_cli();
        Ok(json!({
            "ok": true,
            "repoRoot": path_string(&self.repo_root),
            "assetsRoot": path_string(&self.assets_root),
            "exercismCli": {
                "available": cli.is_some(),
                "path": cli.as_ref().map(|path| path_string(path)),
                "workspace": cli.as_ref().map(|path| exercism_workspace(path)),
                "tokenConfigured": cli.as_ref().map(test_exercism_token).unwrap_or(false),
            },
            "geminiConfigured": self.gemini_key().is_some(),
        }))
    }

    async fn catalog(&self) -> Result<Value> {
        let local = self.local_exercises()?;
        let remote_solutions = self.exercism_solutions().await.unwrap_or_default();
        let mut items = Vec::new();

        for (index, exercise) in self.exercism_catalog().await.into_iter().enumerate() {
            let slug = str_field(&exercise, "slug").unwrap_or_default();
            if slug.is_empty() {
                continue;
            }
            let key = format!("exercism:{slug}");
            let local_entry = local.get(&key);
            let remote = remote_solutions.get(&slug);
            let mut status = local_entry
                .and_then(|entry| str_field(&entry.meta, "status"))
                .unwrap_or_else(|| "available".to_string());
            if status.trim().is_empty() {
                status = "imported".to_string();
            }
            if let Some(solution) = remote {
                if !str_field(solution, "completed_at")
                    .unwrap_or_default()
                    .is_empty()
                {
                    status = "completed".to_string();
                } else if status != "completed" && status != "submitted" {
                    status = "in_progress".to_string();
                }
            }

            items.push(json!({
                "provider": "exercism",
                "providerName": "Exercism C",
                "slug": slug,
                "title": str_field(&exercise, "title").unwrap_or_default(),
                "folderName": safe_file_name(&str_field(&exercise, "title").unwrap_or_default()),
                "difficulty": str_field(&exercise, "difficulty").unwrap_or(Value::Null.to_string()),
                "blurb": str_field(&exercise, "blurb").unwrap_or_default(),
                "iconUrl": str_field(&exercise, "icon_url").unwrap_or_default(),
                "status": status,
                "imported": local_entry.is_some(),
                "folder": local_entry.map(|entry| path_string(&entry.folder)),
                "topics": [],
                "order": index + 1,
                "recommended": bool_field(&exercise, "is_recommended").unwrap_or(false),
                "unlocked": bool_field(&exercise, "is_unlocked").unwrap_or(true),
                "supportsTests": true,
                "supportsSubmit": true,
            }));
        }

        for catalog_name in ["alejandro"] {
            for (index, exercise) in self.static_catalog(catalog_name)?.into_iter().enumerate() {
                let slug = str_field(&exercise, "slug").unwrap_or_default();
                if slug.is_empty() {
                    continue;
                }
                let key = format!("{catalog_name}:{slug}");
                let local_entry = local.get(&key);
                let tests_manifest = local_entry
                    .map(|entry| {
                        entry
                            .folder
                            .join(".estudio-tests")
                            .join("manifest.json")
                            .exists()
                    })
                    .unwrap_or(false);
                items.push(json!({
                    "provider": catalog_name,
                    "providerName": "PDF Alejandro Liz",
                    "slug": slug,
                    "title": str_field(&exercise, "title").unwrap_or_default(),
                    "folderName": safe_file_name(&str_field(&exercise, "title").unwrap_or_default()),
                    "difficulty": str_field(&exercise, "difficulty").unwrap_or_default(),
                    "blurb": str_field(&exercise, "blurb").unwrap_or_default(),
                    "iconUrl": str_field(&exercise, "iconUrl").unwrap_or_default(),
                    "status": local_entry.and_then(|entry| str_field(&entry.meta, "status")).unwrap_or_else(|| "available".to_string()),
                    "imported": local_entry.is_some(),
                    "folder": local_entry.map(|entry| path_string(&entry.folder)),
                    "topics": exercise.get("topics").cloned().unwrap_or_else(|| json!([])),
                    "sourceUrl": exercise.get("sourceUrl").cloned().unwrap_or(Value::Null),
                    "driveFileId": exercise.get("driveFileId").cloned().unwrap_or(Value::Null),
                    "order": index + 1,
                    "unlocked": true,
                    "supportsTests": tests_manifest,
                    "supportsValidate": tests_manifest,
                    "supportsSubmit": false,
                }));
            }
        }

        let cli = exercism_cli();
        Ok(json!({
            "generatedAt": now_iso(),
            "exercismCli": {
                "available": cli.is_some(),
                "path": cli.as_ref().map(|path| path_string(path)),
                "workspace": cli.as_ref().map(|path| exercism_workspace(path)),
                "tokenConfigured": cli.as_ref().map(test_exercism_token).unwrap_or(false),
            },
            "exercises": items,
        }))
    }

    async fn import(&self, params: &Value) -> Result<Value> {
        let provider = string_param(params, "provider").unwrap_or_else(|| "exercism".to_string());
        let slug = string_param(params, "slug").ok_or_else(|| anyhow!("Debes indicar -Slug."))?;
        let force = bool_value(params, "force");
        match provider.as_str() {
            "exercism" => self.import_exercism(&slug, force).await,
            "w3" | "w3schools" | "w3resource" => {
                Err(anyhow!("El proveedor W3 fue eliminado de Estudio Socratico 1.2. Sera reimplementado desde cero en una version futura."))
            }
            other => self.import_template(other, &slug, force).await,
        }
    }

    async fn mark(&self, params: &Value) -> Result<Value> {
        let provider = string_param(params, "provider").unwrap_or_else(|| "exercism".to_string());
        let slug = string_param(params, "slug").ok_or_else(|| anyhow!("Debes indicar -Slug."))?;
        let status = string_param(params, "newStatus")
            .or_else(|| string_param(params, "newstatus"))
            .ok_or_else(|| anyhow!("Debes indicar -NewStatus."))?;

        let local = self.local_exercises()?;
        let key = format!("{provider}:{slug}");
        let entry = local.get(&key).ok_or_else(|| {
            anyhow!("Importa el ejercicio antes de marcarlo como completado o en progreso.")
        })?;
        let meta_path = entry.folder.join(".estudio-exercism.json");
        let mut meta = read_json(&meta_path)?;
        set_json(&mut meta, "status", json!(status));
        if status == "completed" {
            set_json(&mut meta, "completedAt", json!(now_iso()));
        } else {
            set_json(&mut meta, "reopenedAt", json!(now_iso()));
        }
        write_json(&meta_path, &meta)?;
        self.save_progress(
            &provider,
            &slug,
            &str_field(&meta, "title").unwrap_or_default(),
            &status,
            &entry.folder,
        )?;

        Ok(json!({
            "ok": true,
            "provider": provider,
            "slug": slug,
            "title": str_field(&meta, "title").unwrap_or_default(),
            "status": status,
            "folder": path_string(&entry.folder),
        }))
    }

    async fn detect(&self, params: &Value) -> Result<Value> {
        let path = string_param(params, "exercisePath")
            .or_else(|| string_param(params, "exercisepath"))
            .or_else(|| string_param(params, "file"));
        let Some(exercise_root) = self.resolve_exercise_root(path.as_deref())? else {
            return Ok(json!({ "isExercism": false, "isEstudioValidate": false }));
        };
        let meta_path = exercise_root.join(".estudio-exercism.json");
        let provider = if meta_path.exists() {
            read_json(&meta_path)
                .ok()
                .and_then(|meta| str_field(&meta, "provider"))
                .unwrap_or_else(|| "exercism".to_string())
        } else {
            "exercism".to_string()
        };
        let is_validate = provider != "exercism"
            && exercise_root
                .join(".estudio-tests")
                .join("manifest.json")
                .exists();
        Ok(json!({
            "isExercism": provider == "exercism",
            "isEstudioValidate": is_validate,
            "provider": provider,
            "exerciseRoot": path_string(&exercise_root),
        }))
    }

    async fn window_command(&self, params: &Value, action: &str) -> Result<Value> {
        let path = string_param(params, "exercisePath")
            .or_else(|| string_param(params, "exercisepath"))
            .or_else(|| string_param(params, "file"))
            .ok_or_else(|| anyhow!("Debes indicar ExercisePath."))?;
        let exe = engine_bin_path(&self.repo_root);
        Ok(json!({
            "ok": true,
            "action": action,
            "cwd": path_string(&self.repo_root),
            "title": if action == "test" { "Estudio Ejercicios" } else { "Estudio Validacion" },
            "command": {
                "exe": path_string(&exe),
                "args": [action, "--repo-root", &path_string(&self.repo_root), "--assets-root", &path_string(&self.assets_root), "--exercise-path", &path],
            }
        }))
    }

    async fn reveal_tests(&self, params: &Value) -> Result<Value> {
        let path = string_param(params, "exercisePath")
            .or_else(|| string_param(params, "exercisepath"))
            .or_else(|| string_param(params, "file"));
        let exercise_root = self
            .resolve_exercise_root(path.as_deref())?
            .ok_or_else(|| {
                anyhow!("No se pudo detectar un ejercicio importado desde la ruta indicada.")
            })?;
        let tests_root = exercise_root.join(".estudio-tests");
        if !tests_root.exists() {
            return Err(anyhow!("Este ejercicio aun no tiene tests generados."));
        }
        Ok(json!({ "ok": true, "folder": path_string(&tests_root) }))
    }

    async fn import_exercism(&self, slug: &str, overwrite: bool) -> Result<Value> {
        let cli = exercism_cli().ok_or_else(|| anyhow!("No se encontro Exercism CLI. Ejecuta Estudio Socratico Configurador en modo Reparar o instala Exercism.CLI con winget."))?;
        let exercise = self
            .exercism_catalog()
            .await
            .into_iter()
            .find(|item| str_field(item, "slug").as_deref() == Some(slug))
            .ok_or_else(|| {
                anyhow!("No se encontro el ejercicio '{slug}' en el track C de Exercism.")
            })?;
        let title = str_field(&exercise, "title").unwrap_or_else(|| slug.to_string());
        let workspace = exercism_workspace(&cli);
        let source = PathBuf::from(workspace).join("c").join(slug);

        if !source.exists() || overwrite {
            let mut args = vec!["download", "--track", "c", "--exercise", slug];
            if overwrite {
                args.push("--force");
            }
            let (exit_code, output) = run_capture(None, &cli, &args, None).await?;
            if exit_code != 0 {
                return Err(anyhow!(
                    "exercism download fallo para '{slug}'. {}",
                    output.join(" ")
                ));
            }
        }
        if !source.exists() {
            return Err(anyhow!(
                "No encontre el ejercicio descargado en {}.",
                path_string(&source)
            ));
        }

        let folder_name = safe_file_name(&title);
        let target = self.repo_root.join("Ejercicios").join(&folder_name);
        if target.exists() && !overwrite {
            return Err(anyhow!(
                "Ya existe {}. Usa Force si quieres reemplazarlo.",
                path_string(&target)
            ));
        }
        replace_dir(&target)?;
        let support_relative = PathBuf::from(".estudio-exercism").join("support");
        let support_root = target.join(&support_relative);
        fs::create_dir_all(&support_root)?;
        copy_dir_contents(&source, &support_root)?;

        let readme_path = support_root.join("README.md");
        let original_readme = if readme_path.exists() {
            fs::read_to_string(&readme_path)?
        } else {
            format!(
                "# {title}\n\n{}",
                str_field(&exercise, "blurb").unwrap_or_default()
            )
        };
        let translated = self
            .gemini_translation(&original_readme, &title)
            .await
            .unwrap_or_else(|_| select_instruction_markdown(&original_readme));
        fs::write(&readme_path, &translated)?;

        let solution_files = solution_files(&support_root)?;
        for solution in &solution_files {
            let source_file = support_root.join(solution);
            let target_file = target.join(solution);
            if source_file.exists() {
                if let Some(parent) = target_file.parent() {
                    fs::create_dir_all(parent)?;
                }
                fs::copy(&source_file, &target_file)?;
            }
        }
        add_header_to_solutions(
            &target,
            &solution_files,
            &translated,
            &title,
            "Estudio Socratico",
        )?;

        let open_file = solution_files
            .iter()
            .find(|file| file.ends_with(".c"))
            .map(|file| target.join(file))
            .filter(|path| path.exists());
        let meta = json!({
            "provider": "exercism",
            "track": "c",
            "slug": slug,
            "title": title,
            "folderName": folder_name,
            "status": "imported",
            "difficulty": str_field(&exercise, "difficulty").unwrap_or_default(),
            "blurb": str_field(&exercise, "blurb").unwrap_or_default(),
            "iconUrl": str_field(&exercise, "icon_url").unwrap_or_default(),
            "solutionFiles": solution_files,
            "supportRoot": rel_string(&support_relative),
            "importedAt": now_iso(),
            "sourceWorkspace": path_string(&source),
        });
        write_json(&target.join(".estudio-exercism.json"), &meta)?;
        self.save_progress("exercism", slug, &title, "imported", &target)?;

        Ok(json!({
            "ok": true,
            "provider": "exercism",
            "slug": slug,
            "title": title,
            "folder": path_string(&target),
            "openFile": open_file.map(|path| path_string(&path)),
        }))
    }

    async fn import_template(&self, provider: &str, slug: &str, overwrite: bool) -> Result<Value> {
        let exercise = self
            .static_catalog(provider)?
            .into_iter()
            .find(|item| str_field(item, "slug").as_deref() == Some(slug))
            .ok_or_else(|| anyhow!("No se encontro '{slug}' en {provider}."))?;
        let title = str_field(&exercise, "title").unwrap_or_else(|| slug.to_string());
        let folder_name = safe_file_name(&title);
        let target = self.repo_root.join("Ejercicios").join(&folder_name);
        if target.exists() && !overwrite {
            return Err(anyhow!(
                "Ya existe {}. Usa Force si quieres reemplazarlo.",
                path_string(&target)
            ));
        }
        replace_dir(&target)?;
        let support_relative = PathBuf::from(".estudio-exercism").join("support");
        let support_root = target.join(&support_relative);
        fs::create_dir_all(&support_root)?;

        let readme = self.template_markdown(&exercise).await?;
        let translated =
            if provider == "alejandro" && str_field(&exercise, "gistInstructionsUrl").is_some() {
                select_instruction_markdown(&readme)
            } else {
                self.gemini_translation(&readme, &title)
                    .await
                    .unwrap_or_else(|_| select_instruction_markdown(&readme))
            };
        let file_name = template_file_name(&exercise, provider, slug);
        let source_path = target.join(&file_name);
        let source_label = if provider == "alejandro" {
            "Problemas de Programacion - Rolando J. Batista & Alejandro J. Liz".to_string()
        } else {
            str_field(&exercise, "sourceUrl").unwrap_or_else(|| "Estudio Socratico".to_string())
        };
        let comment = c_comment_block(&translated, &title, &source_label);
        if provider == "alejandro" {
            fs::write(&source_path, comment)?;
        } else {
            let starter = str_field(&exercise, "starterCode").unwrap_or_else(|| {
                "#include <stdio.h>\n\nint main(void)\n{\n    return 0;\n}\n".to_string()
            });
            fs::write(&source_path, format!("{comment}{starter}"))?;
        }
        fs::write(support_root.join("README.md"), &translated)?;

        let meta = json!({
            "provider": provider,
            "slug": slug,
            "title": title,
            "folderName": folder_name,
            "status": "in_progress",
            "difficulty": exercise.get("difficulty").cloned().unwrap_or(Value::Null),
            "blurb": exercise.get("blurb").cloned().unwrap_or(Value::Null),
            "topics": exercise.get("topics").cloned().unwrap_or_else(|| json!([])),
            "sourceUrl": exercise.get("sourceUrl").cloned().unwrap_or(Value::Null),
            "driveFileId": exercise.get("driveFileId").cloned().unwrap_or(Value::Null),
            "solutionFiles": [file_name],
            "supportRoot": rel_string(&support_relative),
            "importedAt": now_iso(),
        });
        write_json(&target.join(".estudio-exercism.json"), &meta)?;
        self.save_progress(provider, slug, &title, "in_progress", &target)?;

        Ok(json!({
            "ok": true,
            "provider": provider,
            "slug": slug,
            "title": title,
            "folder": path_string(&target),
            "openFile": path_string(&source_path),
        }))
    }

    async fn template_markdown(&self, exercise: &Value) -> Result<String> {
        if let Some(url) = str_field(exercise, "gistInstructionsUrl") {
            let cache_root = self
                .repo_root
                .join("_estudio")
                .join("soporte")
                .join("runtime")
                .join("gist-cache");
            fs::create_dir_all(&cache_root)?;
            let title = str_field(exercise, "title").unwrap_or_else(|| "ejercicio".to_string());
            let cache_path = cache_root.join(format!("{}.md", to_slug(&title)));
            if !cache_path.exists() {
                if let Ok(response) = self.http.get(url).send().await {
                    if let Ok(text) = response.text().await {
                        let _ = fs::write(&cache_path, text);
                    }
                }
            }
            if cache_path.exists() {
                return Ok(select_instruction_markdown(&fs::read_to_string(
                    cache_path,
                )?));
            }
        }

        if let Some(id) = str_field(exercise, "driveFileId") {
            let cache_root = self
                .repo_root
                .join("_estudio")
                .join("soporte")
                .join("runtime")
                .join("drive-cache");
            fs::create_dir_all(&cache_root)?;
            let title = str_field(exercise, "title").unwrap_or_else(|| "ejercicio".to_string());
            let cache_path = cache_root.join(format!("{}.md", to_slug(&title)));
            if !cache_path.exists() {
                let url = format!("https://drive.google.com/uc?export=download&id={id}");
                let text = self.http.get(url).send().await?.text().await?;
                fs::write(&cache_path, text)?;
            }
            return Ok(select_instruction_markdown(&fs::read_to_string(
                cache_path,
            )?));
        }

        if let Some(markdown) = str_field(exercise, "instructionMarkdown") {
            if !markdown.trim().is_empty() {
                return Ok(select_instruction_markdown(&markdown));
            }
        }

        let title = str_field(exercise, "title").unwrap_or_else(|| "Ejercicio".to_string());
        Err(anyhow!("El ejercicio '{title}' no tiene instrucciones disponibles. Ejecuta bun run alejandro:gists:manifest para regenerar el catalogo."))
    }

    async fn test(&self, params: &Value) -> Result<Value> {
        let path = string_param(params, "exercisePath")
            .or_else(|| string_param(params, "exercisepath"))
            .or_else(|| string_param(params, "file"));
        self.exercism_test(path.as_deref()).await
    }

    async fn validate(&self, params: &Value) -> Result<Value> {
        let path = string_param(params, "exercisePath")
            .or_else(|| string_param(params, "exercisepath"))
            .or_else(|| string_param(params, "file"));
        let exercise_root = self
            .resolve_exercise_root(path.as_deref())?
            .ok_or_else(|| {
                anyhow!("No se pudo detectar un ejercicio importado desde la ruta indicada.")
            })?;
        let meta_path = exercise_root.join(".estudio-exercism.json");
        if !meta_path.exists() {
            return Err(anyhow!("Falta .estudio-exercism.json."));
        }
        let mut meta = read_json(&meta_path)?;
        if str_field(&meta, "provider").as_deref() == Some("exercism") {
            return self.exercism_test(path.as_deref()).await;
        }

        let tests_root = exercise_root.join(".estudio-tests");
        if !tests_root.join("manifest.json").exists() {
            return Err(anyhow!("Este ejercicio aun no tiene tests generados. Usa @test o @validar para crearlos primero."));
        }
        let log = self.exercise_log_context(&meta, &exercise_root, "VALIDACION LOCAL")?;
        let ps_runner = tests_root.join("validar.ps1");
        let cmd_runner = tests_root.join("validar.cmd");
        let (exit_code, output) = if ps_runner.exists() {
            run_capture(
                Some(&exercise_root),
                &PathBuf::from("powershell.exe"),
                &[
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    &path_string(&ps_runner),
                    "-RepoRoot",
                    &path_string(&self.repo_root),
                    "-ExerciseRoot",
                    &path_string(&exercise_root),
                ],
                None,
            )
            .await?
        } else if cmd_runner.exists() {
            run_capture(
                Some(&exercise_root),
                &PathBuf::from("cmd.exe"),
                &[
                    "/c",
                    &path_string(&cmd_runner),
                    &path_string(&self.repo_root),
                    &path_string(&exercise_root),
                ],
                None,
            )
            .await?
        } else {
            return Err(anyhow!(
                "No encontre .estudio-tests\\validar.ps1 ni .estudio-tests\\validar.cmd."
            ));
        };
        append_log(&log, &output, exit_code)?;
        let new_status = if exit_code == 0 {
            "completed"
        } else {
            "tests_failed"
        };
        self.set_local_status(
            &exercise_root,
            &mut meta,
            new_status,
            "lastValidateAt",
            "lastValidateExitCode",
            exit_code,
        )?;
        Ok(json!({ "ok": exit_code == 0, "exitCode": exit_code, "output": output }))
    }

    async fn exercism_test(&self, path: Option<&str>) -> Result<Value> {
        let exercise_root = self.resolve_exercise_root(path)?.ok_or_else(|| {
            anyhow!("No se pudo detectar un ejercicio importado desde la ruta indicada.")
        })?;
        let meta_path = exercise_root.join(".estudio-exercism.json");
        let mut meta = if meta_path.exists() {
            read_json(&meta_path)?
        } else {
            json!({ "provider": "exercism", "slug": file_name(&exercise_root), "title": file_name(&exercise_root) })
        };
        if str_field(&meta, "provider").as_deref() != Some("exercism") {
            return Ok(json!({
                "ok": true,
                "exitCode": 0,
                "output": ["[INFO] Este proveedor aun no tiene tests automaticos. Se usara el compilador normal con F9 para el .c."],
            }));
        }
        let _cli = exercism_cli().ok_or_else(|| anyhow!("No se encontro Exercism CLI."))?;
        let support_root = exercism_support_root(&exercise_root, &meta);
        let solution_files = meta_solution_files(&meta)
            .unwrap_or_else(|| solution_files(&support_root).unwrap_or_default());
        let test_workspace = self.new_test_workspace(
            &exercise_root,
            &support_root,
            &solution_files,
            &str_field(&meta, "slug").unwrap_or_default(),
        )?;
        let make = make_command(&test_workspace)?;
        let log = self.exercise_log_context(&meta, &exercise_root, "INTENTO EXERCISM")?;
        log_c_files(&log, &exercise_root)?;
        append_line(&log, "[EXERCISM TEST]")?;
        append_line(&log, "Running tests via make")?;

        let path_prefix = format!(
            "{};C:\\msys64\\usr\\bin;C:\\msys64\\mingw64\\bin",
            path_string(&test_workspace)
        );
        let result = run_capture(Some(&test_workspace), &make, &["test"], Some(&path_prefix)).await;
        let _ = fs::remove_dir_all(&test_workspace);
        let (exit_code, mut output) = result?;
        output.insert(0, "Running tests via make".to_string());
        append_log(&log, &output, exit_code)?;

        let new_status = if exit_code == 0 {
            "tests_passed"
        } else {
            "tests_failed"
        };
        self.set_local_status(
            &exercise_root,
            &mut meta,
            new_status,
            "lastTestAt",
            "lastTestExitCode",
            exit_code,
        )?;
        let timestamp = Utc::now().format("%Y-%m-%dT%H-%M-%S").to_string();
        let user_slug = self.user_slug();
        let message = format!("intento_{user_slug}_{timestamp}_exercism_exit{exit_code}");
        self.logged_git_commit(&exercise_root, &log, &message);

        Ok(json!({ "ok": exit_code == 0, "exitCode": exit_code, "output": output }))
    }

    async fn submit(&self, params: &Value) -> Result<Value> {
        let path = string_param(params, "exercisePath")
            .or_else(|| string_param(params, "exercisepath"))
            .or_else(|| string_param(params, "file"));
        let exercise_root = self
            .resolve_exercise_root(path.as_deref())?
            .ok_or_else(|| anyhow!("No se pudo detectar el ejercicio a enviar."))?;
        let meta_path = exercise_root.join(".estudio-exercism.json");
        if !meta_path.exists() {
            return Err(anyhow!("Falta .estudio-exercism.json."));
        }
        let mut meta = read_json(&meta_path)?;
        if str_field(&meta, "provider").as_deref() != Some("exercism") {
            return Err(anyhow!(
                "Solo los ejercicios de Exercism se pueden enviar con exercism submit."
            ));
        }
        let cli = exercism_cli().ok_or_else(|| anyhow!("No se encontro Exercism CLI."))?;
        let slug = str_field(&meta, "slug").unwrap_or_default();
        let title = str_field(&meta, "title").unwrap_or_else(|| slug.clone());
        let support_root = exercism_support_root(&exercise_root, &meta);
        let solution_files = meta_solution_files(&meta)
            .unwrap_or_else(|| solution_files(&support_root).unwrap_or_default());
        sync_solution_files(&exercise_root, &support_root, &solution_files)?;

        let workspace_root = str_field(&meta, "sourceWorkspace")
            .map(PathBuf::from)
            .unwrap_or_else(|| {
                PathBuf::from(exercism_workspace(&cli))
                    .join("c")
                    .join(&slug)
            });
        if !workspace_root.exists() {
            return Err(anyhow!("No encontre el workspace real de Exercism para '{slug}'. Reimporta el ejercicio o ejecuta exercism download --track c --exercise {slug}."));
        }
        sync_solution_files(&exercise_root, &workspace_root, &solution_files)?;

        let previous = self.exercism_solution_for_slug(&slug).await;
        let (exit_code, output) =
            run_capture(Some(&workspace_root), &cli, &["submit"], None).await?;
        let remote = if exit_code == 0 {
            self.wait_solution_after_submit(&slug, previous.as_ref())
                .await
        } else {
            None
        };
        let remote_status = remote.as_ref().and_then(solution_tests_status);
        let new_status = if exit_code == 0 {
            if remote_status
                .as_deref()
                .unwrap_or_default()
                .to_ascii_lowercase()
                .contains("fail")
                || remote_status
                    .as_deref()
                    .unwrap_or_default()
                    .to_ascii_lowercase()
                    .contains("error")
            {
                "submit_failed"
            } else if remote
                .as_ref()
                .and_then(|item| str_field(item, "completed_at"))
                .filter(|s| !s.is_empty())
                .is_some()
            {
                "completed"
            } else {
                "submitted"
            }
        } else {
            "submit_failed"
        };
        let view_url = solution_url(remote.as_ref(), &slug, &output);

        set_json(&mut meta, "status", json!(new_status));
        set_json(&mut meta, "lastSubmitAt", json!(now_iso()));
        set_json(&mut meta, "lastSubmitExitCode", json!(exit_code));
        set_json(
            &mut meta,
            "lastSubmitRemoteTestsStatus",
            remote_status
                .clone()
                .map(Value::String)
                .unwrap_or(Value::Null),
        );
        set_json(&mut meta, "lastSubmitUrl", json!(view_url));
        if new_status == "completed" {
            set_json(&mut meta, "completedAt", json!(now_iso()));
        }
        write_json(&meta_path, &meta)?;
        self.save_progress("exercism", &slug, &title, new_status, &exercise_root)?;

        Ok(json!({
            "ok": exit_code == 0,
            "completed": new_status == "completed",
            "provider": "exercism",
            "slug": slug,
            "title": title,
            "status": new_status,
            "exitCode": exit_code,
            "remoteTestsStatus": remote_status,
            "viewUrl": view_url,
            "folder": path_string(&exercise_root),
            "output": output,
        }))
    }

    fn local_exercises(&self) -> Result<HashMap<String, LocalExercise>> {
        let mut result = HashMap::new();
        let root = self.repo_root.join("Ejercicios");
        if !root.exists() {
            return Ok(result);
        }
        for entry in fs::read_dir(root)? {
            let entry = entry?;
            if !entry.file_type()?.is_dir() {
                continue;
            }
            let meta_path = entry.path().join(".estudio-exercism.json");
            if !meta_path.exists() {
                continue;
            }
            if let Ok(meta) = read_json(&meta_path) {
                if let (Some(provider), Some(slug)) =
                    (str_field(&meta, "provider"), str_field(&meta, "slug"))
                {
                    result.insert(
                        format!("{provider}:{slug}"),
                        LocalExercise {
                            folder: entry.path(),
                            meta,
                        },
                    );
                }
            }
        }
        Ok(result)
    }

    fn static_catalog(&self, catalog: &str) -> Result<Vec<Value>> {
        let path = self
            .repo_root
            .join("_estudio")
            .join("soporte")
            .join("exercism")
            .join("catalogs")
            .join(format!("{catalog}.json"));
        if !path.exists() {
            return Ok(Vec::new());
        }
        let raw = read_json(&path)?;
        Ok(raw
            .get("exercises")
            .and_then(Value::as_array)
            .cloned()
            .unwrap_or_default())
    }

    fn fundamentals_catalog(&self) -> Result<Value> {
        self.ensure_fundamentals_db()?;
        Ok(json!({
            "courseId": "fundamentos-c",
            "generatedAt": now_iso(),
            "concepts": self.fundamentals_file("concepts")?,
            "route": self.fundamentals_route()?,
            "categories": self.fundamentals_categories()?,
            "exercises": self.fundamentals_exercises_with_state()?,
            "quizzes": self.fundamentals_quizzes()?,
            "projects": self.fundamentals_projects()?,
            "sources": self.fundamentals_file("sources")?,
            "graph": self.fundamentals_file("graph")?,
            "progress": self.fundamentals_progress()?,
            "recommendations": self.fundamentals_recommendations()?,
            "unlocks": self.fundamentals_unlocks()?,
            "ai": {
                "required": false,
                "preferredBroker": "opencode",
                "fallbackMode": "direct",
                "directByokProviders": ["google", "openai", "anthropic", "openrouter", "groq", "mistral", "ollama", "lmstudio"]
            }
        }))
    }

    fn fundamentals_route(&self) -> Result<Value> {
        self.ensure_fundamentals_db()?;
        let mut route = self.fundamentals_file("route")?;
        let exercises = keyed_by_id(array_from(
            &self.fundamentals_file("exercises")?,
            "exercises",
        ));
        let quizzes = keyed_by_id(array_from(&self.fundamentals_file("quizzes")?, "quizzes"));
        let projects = keyed_by_id(array_from(&self.fundamentals_file("projects")?, "projects"));
        let unlocks = self.fundamentals_unlock_map()?;
        let progress = self.exercise_progress_map()?;

        if let Some(modules) = route.get_mut("modules").and_then(Value::as_array_mut) {
            for module in modules {
                let module_id = str_field(module, "id").unwrap_or_default();
                let module_unlocked = self.module_unlocked(&module_id, &unlocks);
                set_json(module, "unlocked", json!(module_unlocked));
                if let Some(nodes) = module.get_mut("nodes").and_then(Value::as_array_mut) {
                    for node in nodes {
                        let node_type = str_field(node, "type").unwrap_or_default();
                        let reference = str_field(node, "ref").unwrap_or_default();
                        let item = match node_type.as_str() {
                            "quiz" => quizzes.get(&reference),
                            "project" => projects.get(&reference),
                            _ => exercises.get(&reference),
                        };
                        if let Some(item) = item {
                            let unlocked = module_unlocked && self.item_unlocked(item, &unlocks);
                            set_json(node, "unlocked", json!(unlocked));
                            set_json(
                                node,
                                "status",
                                json!(progress
                                    .get(&reference)
                                    .cloned()
                                    .unwrap_or_else(|| "not_started".to_string())),
                            );
                            set_json(node, "item", item.clone());
                        }
                    }
                }
            }
        }
        Ok(route)
    }

    fn fundamentals_categories(&self) -> Result<Value> {
        self.ensure_fundamentals_db()?;
        let mut categories = array_from(&self.fundamentals_file("categories")?, "categories");
        let exercises = self.fundamentals_exercises_with_state()?;
        let projects = self.fundamentals_projects()?;
        let quizzes = self.fundamentals_quizzes()?;

        for category in &mut categories {
            let topic = str_field(category, "topic").unwrap_or_default();
            let exercise_count = exercises
                .iter()
                .filter(|item| item_topics(item).contains(&topic))
                .count();
            let project_count = projects
                .as_array()
                .into_iter()
                .flatten()
                .filter(|item| item_topics(item).contains(&topic))
                .count();
            let quiz_count = quizzes
                .as_array()
                .into_iter()
                .flatten()
                .filter(|item| item_topics(item).contains(&topic))
                .count();
            set_json(category, "exerciseCount", json!(exercise_count));
            set_json(category, "projectCount", json!(project_count));
            set_json(category, "quizCount", json!(quiz_count));
        }

        Ok(json!({
            "courseId": "fundamentos-c",
            "categories": categories,
            "exercises": exercises,
            "projects": projects,
            "quizzes": quizzes
        }))
    }

    fn fundamentals_quizzes(&self) -> Result<Value> {
        self.ensure_fundamentals_db()?;
        let mut quizzes = array_from(&self.fundamentals_file("quizzes")?, "quizzes");
        let unlocks = self.fundamentals_unlock_map()?;
        let quiz_scores = self.quiz_score_map()?;
        for quiz in &mut quizzes {
            let id = str_field(quiz, "id").unwrap_or_default();
            set_json(quiz, "unlocked", json!(self.item_unlocked(quiz, &unlocks)));
            set_json(
                quiz,
                "bestScore",
                quiz_scores
                    .get(&id)
                    .map(|score| json!(score))
                    .unwrap_or(Value::Null),
            );
        }
        Ok(Value::Array(quizzes))
    }

    fn fundamentals_projects(&self) -> Result<Value> {
        self.ensure_fundamentals_db()?;
        let mut projects = array_from(&self.fundamentals_file("projects")?, "projects");
        let unlocks = self.fundamentals_unlock_map()?;
        for project in &mut projects {
            set_json(
                project,
                "unlocked",
                json!(self.item_unlocked(project, &unlocks)),
            );
        }
        Ok(Value::Array(projects))
    }

    fn fundamentals_progress(&self) -> Result<Value> {
        let db = self.ensure_fundamentals_db()?;
        Ok(json!({
            "courseId": "fundamentos-c",
            "dbPath": path_string(&self.fundamentals_db_path()),
            "exerciseProgress": self.query_json_rows(&db, "SELECT exercise_id, status, best_score, attempts, updated_at FROM exercise_progress ORDER BY exercise_id")?,
            "quizAttempts": self.query_json_rows(&db, "SELECT quiz_id, score, passed, attempted_at FROM quiz_attempts ORDER BY attempted_at DESC LIMIT 50")?,
            "conceptMastery": self.query_json_rows(&db, "SELECT concept_id, mastery, updated_at FROM concept_mastery ORDER BY concept_id")?,
            "signals": self.query_json_rows(&db, "SELECT kind, topic, ref_id, weight, message, created_at FROM signals ORDER BY created_at DESC LIMIT 50")?,
            "recommendations": self.query_json_rows(&db, "SELECT recommendation_id, kind, ref_id, reason, score, created_at, dismissed_at FROM recommendations ORDER BY created_at DESC LIMIT 20")?,
            "unlocks": self.query_json_rows(&db, "SELECT item_type, item_id, unlocked, reason, updated_at FROM unlocks ORDER BY item_type, item_id")?
        }))
    }

    fn fundamentals_recommendations(&self) -> Result<Value> {
        self.ensure_fundamentals_db()?;
        let route = self.fundamentals_route()?;
        let projects = self.fundamentals_projects()?;
        let quizzes = self.fundamentals_quizzes()?;
        let mastery = self.concept_mastery_map()?;
        let mut recommendations = Vec::new();

        if let Some(next) = next_route_node(&route) {
            recommendations.push(json!({
                "id": format!("next-route-{}", str_field(&next, "ref").unwrap_or_default()),
                "kind": "next-route",
                "refId": str_field(&next, "ref").unwrap_or_default(),
                "title": str_field(&next, "title").unwrap_or_else(|| "Continua la Ruta C".to_string()),
                "reason": "Siguiente paso desbloqueado en Ruta C.",
                "score": 90
            }));
        }

        for project in projects.as_array().into_iter().flatten() {
            if bool_field(project, "unlocked").unwrap_or(false) {
                recommendations.push(json!({
                    "id": format!("project-{}", str_field(project, "id").unwrap_or_default()),
                    "kind": "project-unlocked",
                    "refId": str_field(project, "id").unwrap_or_default(),
                    "title": str_field(project, "title").unwrap_or_default(),
                    "reason": "Ya tienes base suficiente para intentar esta asignacion o proyecto.",
                    "score": 72
                }));
                break;
            }
        }

        for quiz in quizzes.as_array().into_iter().flatten() {
            if bool_field(quiz, "unlocked").unwrap_or(false) {
                recommendations.push(json!({
                    "id": format!("quiz-{}", str_field(quiz, "id").unwrap_or_default()),
                    "kind": "quiz-recommended",
                    "refId": str_field(quiz, "id").unwrap_or_default(),
                    "title": str_field(quiz, "title").unwrap_or_default(),
                    "reason": "Quiz teorico disponible para comprobar dominio bajo presion.",
                    "score": 64
                }));
                break;
            }
        }

        for (topic, value) in mastery {
            if value < 0.5 {
                recommendations.push(json!({
                    "id": format!("reinforce-{topic}"),
                    "kind": "reinforcement",
                    "topic": topic,
                    "reason": "Dominio local bajo; conviene practicar antes de avanzar demasiado.",
                    "score": 55
                }));
                break;
            }
        }

        self.persist_recommendations(&recommendations)?;
        Ok(json!({
            "courseId": "fundamentos-c",
            "generatedAt": now_iso(),
            "recommendations": recommendations
        }))
    }

    fn fundamentals_unlocks(&self) -> Result<Value> {
        self.ensure_fundamentals_db()?;
        let exercises = array_from(&self.fundamentals_file("exercises")?, "exercises");
        let quizzes = array_from(&self.fundamentals_file("quizzes")?, "quizzes");
        let projects = array_from(&self.fundamentals_file("projects")?, "projects");
        let unlocks = self.fundamentals_unlock_map()?;
        let mut items = Vec::new();
        for (kind, list) in [
            ("exercise", exercises),
            ("quiz", quizzes),
            ("project", projects),
        ] {
            for item in list {
                let id = str_field(&item, "id").unwrap_or_default();
                let unlocked = self.item_unlocked(&item, &unlocks);
                items.push(json!({
                    "itemType": kind,
                    "itemId": id,
                    "unlocked": unlocked,
                    "reason": if unlocked { "prerequisitos satisfechos" } else { "faltan prerequisitos" }
                }));
            }
        }
        self.persist_unlocks(&items)?;
        Ok(json!({
            "courseId": "fundamentos-c",
            "unlocks": items
        }))
    }

    fn fundamentals_exercises_with_state(&self) -> Result<Vec<Value>> {
        self.ensure_fundamentals_db()?;
        let mut exercises = array_from(&self.fundamentals_file("exercises")?, "exercises");
        let unlocks = self.fundamentals_unlock_map()?;
        let progress = self.exercise_progress_map()?;
        for exercise in &mut exercises {
            let id = str_field(exercise, "id").unwrap_or_default();
            let topics = item_topics(exercise);
            set_json(exercise, "topics", json!(topics));
            set_json(
                exercise,
                "unlocked",
                json!(self.item_unlocked(exercise, &unlocks)),
            );
            set_json(
                exercise,
                "status",
                json!(progress
                    .get(&id)
                    .cloned()
                    .unwrap_or_else(|| "not_started".to_string())),
            );
        }
        Ok(exercises)
    }

    fn fundamentals_file(&self, name: &str) -> Result<Value> {
        let path = self
            .assets_root
            .join("catalog")
            .join("fundamentos-c")
            .join(format!("{name}.json"));
        read_json(&path)
    }

    fn fundamentals_db_path(&self) -> PathBuf {
        self.repo_root
            .join("usuario")
            .join("progreso")
            .join("fundamentos-c")
            .join("estudio.db")
    }

    fn ensure_fundamentals_db(&self) -> Result<Connection> {
        let path = self.fundamentals_db_path();
        if let Some(parent) = path.parent() {
            fs::create_dir_all(parent)?;
        }
        let db = Connection::open(path)?;
        db.execute_batch(
            r#"
            CREATE TABLE IF NOT EXISTS attempts (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                item_type TEXT NOT NULL,
                item_id TEXT NOT NULL,
                status TEXT NOT NULL,
                score REAL,
                created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS exercise_progress (
                exercise_id TEXT PRIMARY KEY,
                status TEXT NOT NULL DEFAULT 'not_started',
                best_score REAL,
                attempts INTEGER NOT NULL DEFAULT 0,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS quiz_attempts (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                quiz_id TEXT NOT NULL,
                score REAL NOT NULL,
                passed INTEGER NOT NULL,
                attempted_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS concept_mastery (
                concept_id TEXT PRIMARY KEY,
                mastery REAL NOT NULL DEFAULT 0,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS signals (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                kind TEXT NOT NULL,
                topic TEXT,
                ref_id TEXT,
                weight REAL NOT NULL DEFAULT 1,
                message TEXT,
                created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS recommendations (
                recommendation_id TEXT PRIMARY KEY,
                kind TEXT NOT NULL,
                ref_id TEXT,
                reason TEXT,
                score REAL NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                dismissed_at TEXT
            );
            CREATE TABLE IF NOT EXISTS unlocks (
                item_type TEXT NOT NULL,
                item_id TEXT NOT NULL,
                unlocked INTEGER NOT NULL,
                reason TEXT,
                updated_at TEXT NOT NULL,
                PRIMARY KEY (item_type, item_id)
            );
            "#,
        )?;

        let concepts = array_from(&self.fundamentals_file("concepts")?, "concepts");
        for concept in concepts {
            if let Some(id) = str_field(&concept, "id") {
                db.execute(
                    "INSERT OR IGNORE INTO concept_mastery (concept_id, mastery, updated_at) VALUES (?1, 0, ?2)",
                    params![id, now_iso()],
                )?;
            }
        }
        Ok(db)
    }

    fn query_json_rows(&self, db: &Connection, sql: &str) -> Result<Value> {
        let mut stmt = db.prepare(sql)?;
        let names: Vec<String> = stmt
            .column_names()
            .iter()
            .map(|name| name.to_string())
            .collect();
        let rows = stmt.query_map([], |row| {
            let mut object = Map::new();
            for (index, name) in names.iter().enumerate() {
                let value = row.get_ref(index)?;
                let json_value = match value {
                    rusqlite::types::ValueRef::Null => Value::Null,
                    rusqlite::types::ValueRef::Integer(v) => json!(v),
                    rusqlite::types::ValueRef::Real(v) => json!(v),
                    rusqlite::types::ValueRef::Text(v) => {
                        Value::String(String::from_utf8_lossy(v).to_string())
                    }
                    rusqlite::types::ValueRef::Blob(_) => Value::Null,
                };
                object.insert(name.clone(), json_value);
            }
            Ok(Value::Object(object))
        })?;
        let mut result = Vec::new();
        for row in rows {
            result.push(row?);
        }
        Ok(Value::Array(result))
    }

    fn exercise_progress_map(&self) -> Result<HashMap<String, String>> {
        let db = self.ensure_fundamentals_db()?;
        let mut stmt = db.prepare("SELECT exercise_id, status FROM exercise_progress")?;
        let mut rows = stmt.query([])?;
        let mut result = HashMap::new();
        while let Some(row) = rows.next()? {
            result.insert(row.get::<_, String>(0)?, row.get::<_, String>(1)?);
        }
        Ok(result)
    }

    fn quiz_score_map(&self) -> Result<HashMap<String, f64>> {
        let db = self.ensure_fundamentals_db()?;
        let mut stmt =
            db.prepare("SELECT quiz_id, MAX(score) FROM quiz_attempts GROUP BY quiz_id")?;
        let mut rows = stmt.query([])?;
        let mut result = HashMap::new();
        while let Some(row) = rows.next()? {
            result.insert(row.get::<_, String>(0)?, row.get::<_, f64>(1)?);
        }
        Ok(result)
    }

    fn concept_mastery_map(&self) -> Result<HashMap<String, f64>> {
        let db = self.ensure_fundamentals_db()?;
        let mut stmt = db.prepare("SELECT concept_id, mastery FROM concept_mastery")?;
        let mut rows = stmt.query([])?;
        let mut result = HashMap::new();
        while let Some(row) = rows.next()? {
            result.insert(row.get::<_, String>(0)?, row.get::<_, f64>(1)?);
        }
        Ok(result)
    }

    fn fundamentals_unlock_map(&self) -> Result<HashMap<String, bool>> {
        let progress = self.exercise_progress_map()?;
        let quiz_scores = self.quiz_score_map()?;
        let mastery = self.concept_mastery_map()?;
        let route = self.fundamentals_file("route")?;
        let mut completed_modules = HashMap::new();
        if let Some(modules) = route.get("modules").and_then(Value::as_array) {
            for module in modules {
                let module_id = str_field(module, "id").unwrap_or_default();
                let refs: Vec<String> = module
                    .get("nodes")
                    .and_then(Value::as_array)
                    .into_iter()
                    .flatten()
                    .filter(|node| str_field(node, "type").as_deref() == Some("exercise"))
                    .filter_map(|node| str_field(node, "ref"))
                    .collect();
                let complete = !refs.is_empty()
                    && refs.iter().all(|id| {
                        matches!(
                            progress.get(id).map(String::as_str),
                            Some("completed" | "tests_passed" | "submitted")
                        )
                    });
                completed_modules.insert(module_id, complete);
            }
        }

        let mut result = HashMap::new();
        let mut all_items = Vec::new();
        all_items.extend(array_from(
            &self.fundamentals_file("exercises")?,
            "exercises",
        ));
        all_items.extend(array_from(&self.fundamentals_file("quizzes")?, "quizzes"));
        all_items.extend(array_from(&self.fundamentals_file("projects")?, "projects"));
        for item in all_items {
            if let Some(id) = str_field(&item, "id") {
                let unlocked = item_unlock_satisfied(
                    &item,
                    &progress,
                    &quiz_scores,
                    &mastery,
                    &completed_modules,
                );
                result.insert(id, unlocked);
            }
        }
        Ok(result)
    }

    fn module_unlocked(&self, module_id: &str, unlocks: &HashMap<String, bool>) -> bool {
        if module_id == "primeros-programas" {
            return true;
        }
        let route = self
            .fundamentals_file("route")
            .unwrap_or_else(|_| json!({}));
        route
            .get("modules")
            .and_then(Value::as_array)
            .into_iter()
            .flatten()
            .find(|module| str_field(module, "id").as_deref() == Some(module_id))
            .and_then(|module| module.get("nodes").and_then(Value::as_array))
            .map(|nodes| {
                nodes
                    .iter()
                    .filter_map(|node| str_field(node, "ref"))
                    .any(|id| unlocks.get(&id).copied().unwrap_or(false))
            })
            .unwrap_or(false)
    }

    fn item_unlocked(&self, item: &Value, unlocks: &HashMap<String, bool>) -> bool {
        str_field(item, "id")
            .and_then(|id| unlocks.get(&id).copied())
            .unwrap_or(false)
    }

    fn persist_recommendations(&self, recommendations: &[Value]) -> Result<()> {
        let db = self.ensure_fundamentals_db()?;
        for recommendation in recommendations {
            let id = str_field(recommendation, "id").unwrap_or_else(|| to_slug(&now_iso()));
            db.execute(
                "INSERT OR REPLACE INTO recommendations (recommendation_id, kind, ref_id, reason, score, created_at, dismissed_at) VALUES (?1, ?2, ?3, ?4, ?5, ?6, NULL)",
                params![
                    id,
                    str_field(recommendation, "kind").unwrap_or_default(),
                    str_field(recommendation, "refId").or_else(|| str_field(recommendation, "topic")),
                    str_field(recommendation, "reason").unwrap_or_default(),
                    int_value(recommendation, "score").unwrap_or(0) as f64,
                    now_iso()
                ],
            )?;
        }
        Ok(())
    }

    fn persist_unlocks(&self, unlocks: &[Value]) -> Result<()> {
        let db = self.ensure_fundamentals_db()?;
        for item in unlocks {
            db.execute(
                "INSERT OR REPLACE INTO unlocks (item_type, item_id, unlocked, reason, updated_at) VALUES (?1, ?2, ?3, ?4, ?5)",
                params![
                    str_field(item, "itemType").unwrap_or_default(),
                    str_field(item, "itemId").unwrap_or_default(),
                    if bool_field(item, "unlocked").unwrap_or(false) { 1 } else { 0 },
                    str_field(item, "reason").unwrap_or_default(),
                    now_iso()
                ],
            )?;
        }
        Ok(())
    }

    async fn exercism_catalog(&self) -> Vec<Value> {
        let fallback = vec![
            json!({"slug":"hello-world","title":"Hello World","difficulty":"easy","blurb":"Exercism's classic introductory exercise.","icon_url":""}),
            json!({"slug":"lasagna","title":"Lasagna","difficulty":"easy","blurb":"Learn about basics by helping cook lasagna.","icon_url":""}),
            json!({"slug":"grains","title":"Grains","difficulty":"easy","blurb":"Calculate grains of wheat on a chessboard.","icon_url":""}),
            json!({"slug":"collatz-conjecture","title":"Collatz Conjecture","difficulty":"easy","blurb":"Calculate the number of steps to reach 1.","icon_url":""}),
        ];
        let mut request = self
            .http
            .get("https://api.exercism.org/v2/tracks/c/exercises");
        if let Some(token) = exercism_token() {
            request = request.bearer_auth(token);
        }
        match request.send().await {
            Ok(response) => match response.json::<Value>().await {
                Ok(raw) => raw
                    .get("exercises")
                    .and_then(Value::as_array)
                    .cloned()
                    .unwrap_or(fallback),
                Err(_) => fallback,
            },
            Err(_) => fallback,
        }
    }

    async fn exercism_solutions(&self) -> Result<HashMap<String, Value>> {
        let Some(token) = exercism_token() else {
            return Ok(HashMap::new());
        };
        let raw = self
            .http
            .get("https://api.exercism.org/v2/solutions?track_slug=c")
            .bearer_auth(token)
            .send()
            .await?
            .json::<Value>()
            .await?;
        let mut result = HashMap::new();
        for solution in raw
            .get("results")
            .and_then(Value::as_array)
            .into_iter()
            .flatten()
        {
            if let Some(slug) = solution.get("exercise").and_then(|e| str_field(e, "slug")) {
                result.insert(slug, solution.clone());
            }
        }
        Ok(result)
    }

    async fn exercism_solution_for_slug(&self, slug: &str) -> Option<Value> {
        self.exercism_solutions().await.ok()?.remove(slug)
    }

    async fn wait_solution_after_submit(
        &self,
        slug: &str,
        previous: Option<&Value>,
    ) -> Option<Value> {
        let previous_iterations = previous
            .and_then(|v| int_value(v, "num_iterations"))
            .unwrap_or(-1);
        let previous_last = previous
            .and_then(|v| str_field(v, "last_iterated_at"))
            .unwrap_or_default();
        let previous_updated = previous
            .and_then(|v| str_field(v, "updated_at"))
            .unwrap_or_default();
        let mut latest = None;
        let mut saw_changed = previous.is_none();
        for _ in 0..8 {
            latest = self.exercism_solution_for_slug(slug).await;
            if let Some(solution) = latest.as_ref() {
                let changed = previous.is_none()
                    || int_value(solution, "num_iterations").unwrap_or(-1) > previous_iterations
                    || str_field(solution, "last_iterated_at").unwrap_or_default() != previous_last
                    || str_field(solution, "updated_at").unwrap_or_default() != previous_updated;
                if changed {
                    saw_changed = true;
                }
                let status = solution_tests_status(solution)
                    .unwrap_or_default()
                    .to_ascii_lowercase();
                let completed = str_field(solution, "completed_at")
                    .map(|s| !s.is_empty())
                    .unwrap_or(false);
                if changed
                    && (completed
                        || status.contains("passed")
                        || status.contains("failed")
                        || status.contains("error"))
                {
                    return latest;
                }
            }
            sleep(Duration::from_secs(2)).await;
        }
        if saw_changed {
            latest
        } else {
            None
        }
    }

    async fn gemini_translation(&self, markdown: &str, title: &str) -> Result<String> {
        let markdown = select_instruction_markdown(markdown);
        let Some(api_key) = self.gemini_key() else {
            return Ok(format!(
                "# {title}\n\n> Traduccion automatica pendiente.\n\nConfigura la API Key local de la extension o la variable de entorno `GEMINI_API_KEY` y vuelve a importar este ejercicio para generar las instrucciones en espanol.\n\nMientras tanto, usa los tests del ejercicio como guia de comportamiento esperado.\n"
            ));
        };
        let model = self.gemini_model();
        let system_prompt = "Eres un traductor tecnico para estudiantes de programacion en C.\nDevuelve unicamente la traduccion solicitada.\nNo saludes, no expliques lo que hiciste, no anadas introducciones, no cierres con notas y no uses frases como \"Aqui tienes\".\nConserva Markdown, tablas, listas, nombres de funciones, nombres de archivos y bloques de codigo.\nNo resuelvas el ejercicio ni agregues pistas.";
        let prompt = format!("Traduce al espanol latinoamericano el siguiente enunciado de un ejercicio de programacion.\nDevuelve solo el Markdown traducido, sin introducciones ni explicaciones.\nTitulo del ejercicio: {title}\n\n{markdown}");
        let body = json!({
            "systemInstruction": { "parts": [{ "text": system_prompt }] },
            "contents": [{ "role": "user", "parts": [{ "text": prompt }] }],
            "generationConfig": { "temperature": 0.1 }
        });
        let url = format!("https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={api_key}");
        let raw = self
            .http
            .post(url)
            .json(&body)
            .send()
            .await?
            .json::<Value>()
            .await?;
        let text = raw
            .pointer("/candidates/0/content/parts/0/text")
            .and_then(Value::as_str)
            .ok_or_else(|| anyhow!("Gemini no devolvio texto traducido."))?;
        Ok(clear_translation_text(text))
    }

    fn gemini_key(&self) -> Option<String> {
        for path in [
            self.repo_root
                .join("usuario")
                .join("config")
                .join("estudio-socratico.extension.local.json"),
            self.repo_root
                .join("_estudio")
                .join("soporte")
                .join("exercism")
                .join("config.local.json"),
            self.repo_root
                .join("_estudio")
                .join("soporte")
                .join("exercism")
                .join("config.json"),
            self.repo_root.join(".estudio_exercism.local.json"),
        ] {
            if let Ok(config) = read_json(&path) {
                let gemini = config.get("gemini").unwrap_or(&config);
                for key in ["apiKey", "geminiApiKey", "GEMINI_API_KEY"] {
                    if let Some(value) = str_field(gemini, key).filter(|v| !v.trim().is_empty()) {
                        return Some(value);
                    }
                }
            }
        }
        env::var("GEMINI_API_KEY")
            .ok()
            .filter(|value| !value.trim().is_empty())
    }

    fn gemini_model(&self) -> String {
        for path in [
            self.repo_root
                .join("usuario")
                .join("config")
                .join("estudio-socratico.extension.local.json"),
            self.repo_root
                .join("_estudio")
                .join("soporte")
                .join("exercism")
                .join("config.local.json"),
            self.repo_root
                .join("_estudio")
                .join("soporte")
                .join("exercism")
                .join("config.json"),
            self.repo_root.join(".estudio_exercism.local.json"),
        ] {
            if let Ok(config) = read_json(&path) {
                let gemini = config.get("gemini").unwrap_or(&config);
                if let Some(model) =
                    str_field(gemini, "model").or_else(|| str_field(gemini, "geminiModel"))
                {
                    if !model.trim().is_empty() {
                        return model;
                    }
                }
            }
        }
        env::var("GEMINI_MODEL").unwrap_or_else(|_| "gemini-2.5-flash-lite".to_string())
    }

    fn resolve_exercise_root(&self, path: Option<&str>) -> Result<Option<PathBuf>> {
        let Some(path) = path.filter(|p| !p.trim().is_empty()) else {
            return Ok(None);
        };
        let item = PathBuf::from(path);
        if !item.exists() {
            return Ok(None);
        }
        let mut dir = if item.is_dir() {
            item
        } else {
            item.parent().map(Path::to_path_buf).unwrap_or(item)
        };
        let root_text = path_string(&self.repo_root).to_ascii_lowercase();
        loop {
            if imported_markers(&dir) {
                return Ok(Some(dir));
            }
            if !path_string(&dir)
                .to_ascii_lowercase()
                .starts_with(&root_text)
            {
                return Ok(None);
            }
            if !dir.pop() {
                break;
            }
        }
        Ok(None)
    }

    fn save_progress(
        &self,
        provider: &str,
        slug: &str,
        title: &str,
        status: &str,
        folder: &Path,
    ) -> Result<()> {
        let user_root = self.user_root(true)?;
        let dir = user_root.join("exercism");
        fs::create_dir_all(&dir)?;
        let path = dir.join("progreso.json");
        let mut data = if path.exists() {
            read_json(&path).unwrap_or_else(|_| json!({}))
        } else {
            json!({})
        };
        if !data.is_object() {
            data = json!({});
        }
        let key = format!("{provider}:{slug}");
        set_json(
            &mut data,
            &key,
            json!({
                "provider": provider,
                "slug": slug,
                "title": title,
                "status": status,
                "folder": path_string(folder),
                "updatedAt": now_iso(),
            }),
        );
        write_json(&path, &data)
    }

    fn user_slug(&self) -> String {
        let config = self.repo_root.join(".estudio_usuario");
        if let Ok(value) = fs::read_to_string(config) {
            if let Some(line) = value.lines().find(|line| !line.trim().is_empty()) {
                return to_slug(line);
            }
        }
        let output = std::process::Command::new("git")
            .args([
                "-C",
                &path_string(&self.repo_root),
                "branch",
                "--show-current",
            ])
            .output();
        if let Ok(output) = output {
            let branch = String::from_utf8_lossy(&output.stdout).trim().to_string();
            if !branch.is_empty() {
                return to_slug(&branch);
            }
        }
        to_slug(&env::var("USERNAME").unwrap_or_else(|_| "usuario".to_string()))
    }

    fn user_root(&self, create: bool) -> Result<PathBuf> {
        let canonical = self.repo_root.join("usuario");
        if canonical.exists() {
            return Ok(canonical);
        }
        let legacy = self
            .repo_root
            .join("_estudio")
            .join("usuarios")
            .join(self.user_slug());
        if legacy.exists() {
            if create {
                fs::rename(&legacy, &canonical)?;
                return Ok(canonical);
            }
            return Ok(legacy);
        }
        if create {
            fs::create_dir_all(&canonical)?;
        }
        Ok(canonical)
    }

    fn exercise_log_context(
        &self,
        meta: &Value,
        exercise_root: &Path,
        label: &str,
    ) -> Result<PathBuf> {
        let user_root = self.user_root(true)?;
        let title = str_field(meta, "title").unwrap_or_else(|| file_name(exercise_root));
        let logs_dir = user_root.join("logs").join(to_slug(&title));
        fs::create_dir_all(&logs_dir)?;
        let errores = user_root.join("errores.md");
        if !errores.exists() {
            fs::write(&errores, "")?;
        }
        let timestamp = Utc::now().format("%Y-%m-%dT%H-%M-%S").to_string();
        let log = logs_dir.join(format!("bloque_{timestamp}.log"));
        append_line(
            &log,
            "============================================================",
        )?;
        append_line(&log, &format!("{label}: {timestamp}"))?;
        append_line(&log, &format!("EJERCICIO: {title}"))?;
        append_line(&log, &format!("RUTA: {}", path_string(exercise_root)))?;
        append_line(
            &log,
            "============================================================",
        )?;
        Ok(log)
    }

    fn set_local_status(
        &self,
        exercise_root: &Path,
        meta: &mut Value,
        status: &str,
        time_field: &str,
        exit_field: &str,
        exit_code: i32,
    ) -> Result<()> {
        let meta_path = exercise_root.join(".estudio-exercism.json");
        if meta_path.exists() {
            set_json(meta, "status", json!(status));
            set_json(meta, time_field, json!(now_iso()));
            set_json(meta, exit_field, json!(exit_code));
            if status == "completed" {
                set_json(meta, "completedAt", json!(now_iso()));
            }
            write_json(&meta_path, meta)?;
            if let (Some(provider), Some(slug), Some(title)) = (
                str_field(meta, "provider"),
                str_field(meta, "slug"),
                str_field(meta, "title"),
            ) {
                self.save_progress(&provider, &slug, &title, status, exercise_root)?;
            }
        }
        Ok(())
    }

    fn new_test_workspace(
        &self,
        exercise_root: &Path,
        support_root: &Path,
        solution_files: &[String],
        slug: &str,
    ) -> Result<PathBuf> {
        let runtime = self
            .repo_root
            .join("_estudio")
            .join("soporte")
            .join("runtime")
            .join("exercism-tests");
        fs::create_dir_all(&runtime)?;
        let stamp = Utc::now().format("%Y%m%d_%H%M%S_%3f").to_string();
        let workspace = runtime.join(format!("{}_{}", to_slug(slug), stamp));
        fs::create_dir_all(&workspace)?;
        copy_dir_contents(support_root, &workspace)?;
        sync_solution_files(exercise_root, &workspace, solution_files)?;
        ensure_make_shim(&workspace)?;
        disable_test_ignore(&workspace)?;
        Ok(workspace)
    }

    fn logged_git_commit(&self, exercise_root: &Path, log: &Path, message: &str) {
        let rel_exercise = rel_to(&self.repo_root, exercise_root);
        let rel_log = rel_to(&self.repo_root, log);
        let errores = self
            .user_root(true)
            .unwrap_or_else(|_| self.repo_root.join("usuario"))
            .join("errores.md");
        let rel_errores = rel_to(&self.repo_root, &errores);
        let _ = std::process::Command::new("git")
            .args([
                "-C",
                &path_string(&self.repo_root),
                "add",
                "--",
                &rel_exercise,
                &rel_log,
                &rel_errores,
            ])
            .status();
        let diff = std::process::Command::new("git")
            .args([
                "-C",
                &path_string(&self.repo_root),
                "diff",
                "--cached",
                "--quiet",
            ])
            .status();
        if diff.map(|status| status.success()).unwrap_or(true) {
            return;
        }
        let (name, email) = git_author(&self.repo_root, &self.user_slug());
        let _ = std::process::Command::new("git")
            .args([
                "-C",
                &path_string(&self.repo_root),
                "-c",
                &format!("user.name={name}"),
                "-c",
                &format!("user.email={email}"),
                "commit",
                "-m",
                message,
            ])
            .status();
    }
}

fn parse_direct_invocation(args: Vec<String>) -> (String, Value) {
    if args.is_empty() {
        return ("catalog".to_string(), json!({}));
    }
    let mut args = args;
    let method = if args[0].starts_with('-') {
        "catalog".to_string()
    } else {
        args.remove(0).to_ascii_lowercase()
    };
    let mut params =
        parse_cli_params(&args)
            .into_iter()
            .fold(Map::new(), |mut map, (key, value)| {
                map.insert(camel_param(&key), Value::String(value));
                map
            });
    for flag in ["force", "json"] {
        if params.get(flag).and_then(Value::as_str) == Some("true") {
            params.insert(flag.to_string(), Value::Bool(true));
        }
    }
    let method = params
        .remove("action")
        .and_then(|value| value.as_str().map(|s| s.to_ascii_lowercase()))
        .unwrap_or(method);
    (method, Value::Object(params))
}

fn parse_cli_params(args: &[String]) -> HashMap<String, String> {
    let mut result = HashMap::new();
    let mut index = 0;
    while index < args.len() {
        let item = &args[index];
        if item.starts_with('-') {
            let key = item.trim_start_matches('-').to_ascii_lowercase();
            if index + 1 < args.len() && !args[index + 1].starts_with('-') {
                result.insert(key, args[index + 1].clone());
                index += 2;
            } else {
                result.insert(key, "true".to_string());
                index += 1;
            }
        } else {
            index += 1;
        }
    }
    result
}

fn resolve_repo_root(root: Option<&str>) -> Result<PathBuf> {
    if let Some(root) = root.filter(|r| !r.trim().is_empty()) {
        let path = PathBuf::from(root);
        if path.join("AGENTS.md").exists() {
            return Ok(clean_path(if path.is_absolute() {
                path
            } else {
                env::current_dir()?.join(path)
            }));
        }
    }
    let mut candidate = env::current_dir()?;
    loop {
        if candidate.join("AGENTS.md").exists() {
            return Ok(candidate);
        }
        if !candidate.pop() {
            break;
        }
    }
    Ok(env::current_dir()?)
}

fn resolve_assets_root(root: Option<&str>, repo_root: &Path) -> Result<PathBuf> {
    if let Some(root) = root.filter(|r| !r.trim().is_empty()) {
        let path = PathBuf::from(root);
        let full = clean_path(if path.is_absolute() {
            path
        } else {
            env::current_dir()?.join(path)
        });
        return Ok(full);
    }

    let exe_assets = env::current_exe()
        .ok()
        .and_then(|path| path.parent().and_then(Path::parent).map(Path::to_path_buf))
        .filter(|path| path.join("catalog").join("fundamentos-c").exists());
    if let Some(path) = exe_assets {
        return Ok(clean_path(path));
    }

    let dev_assets = repo_root.join("_estudio").join("soporte");
    if dev_assets.join("catalog").join("fundamentos-c").exists() {
        return Ok(clean_path(dev_assets));
    }

    Ok(clean_path(repo_root.to_path_buf()))
}

fn clean_path(path: PathBuf) -> PathBuf {
    let mut clean = PathBuf::new();
    for component in path.components() {
        match component {
            std::path::Component::CurDir => {}
            std::path::Component::ParentDir => {
                clean.pop();
            }
            other => clean.push(other.as_os_str()),
        }
    }
    clean
}

fn engine_bin_path(root: &Path) -> PathBuf {
    root.join("_estudio")
        .join("soporte")
        .join("engine")
        .join("bin")
        .join("estudio-engine.exe")
}

fn read_json(path: &Path) -> Result<Value> {
    let text =
        fs::read_to_string(path).with_context(|| format!("Leyendo {}", path_string(path)))?;
    Ok(serde_json::from_str(&text)?)
}

fn array_from(value: &Value, key: &str) -> Vec<Value> {
    value
        .get(key)
        .and_then(Value::as_array)
        .cloned()
        .unwrap_or_default()
}

fn keyed_by_id(items: Vec<Value>) -> HashMap<String, Value> {
    items
        .into_iter()
        .filter_map(|item| str_field(&item, "id").map(|id| (id, item)))
        .collect()
}

fn string_array(value: &Value, key: &str) -> Vec<String> {
    value
        .get(key)
        .and_then(Value::as_array)
        .into_iter()
        .flatten()
        .filter_map(Value::as_str)
        .map(String::from)
        .collect()
}

fn item_topics(item: &Value) -> Vec<String> {
    let mut topics = Vec::new();
    for key in ["primaryTopics", "supportTopics", "combinedTopics", "topics"] {
        for topic in string_array(item, key) {
            if !topics.contains(&topic) {
                topics.push(topic);
            }
        }
    }
    topics
}

fn item_unlock_satisfied(
    item: &Value,
    progress: &HashMap<String, String>,
    quiz_scores: &HashMap<String, f64>,
    mastery: &HashMap<String, f64>,
    completed_modules: &HashMap<String, bool>,
) -> bool {
    let unlock = item.get("unlock").unwrap_or(&Value::Null);

    for exercise in string_array(unlock, "requiresExercises") {
        if !matches!(
            progress.get(&exercise).map(String::as_str),
            Some("completed" | "tests_passed" | "submitted")
        ) {
            return false;
        }
    }

    for module_or_topic in string_array(unlock, "requiresModules") {
        let module_ok = completed_modules
            .get(&module_or_topic)
            .copied()
            .unwrap_or(false);
        let topic_ok = mastery.get(&module_or_topic).copied().unwrap_or(0.0) >= 0.6;
        if !module_ok && !topic_ok {
            return false;
        }
    }

    if let Some(required) = unlock
        .get("requiresQuizScore")
        .filter(|value| !value.is_null())
    {
        let quiz_id = str_field(required, "quizId").unwrap_or_default();
        let min_score = int_value(required, "minScore").unwrap_or(0) as f64;
        if quiz_scores.get(&quiz_id).copied().unwrap_or(0.0) < min_score {
            return false;
        }
    }

    true
}

fn next_route_node(route: &Value) -> Option<Value> {
    route
        .get("modules")
        .and_then(Value::as_array)?
        .iter()
        .flat_map(|module| {
            module
                .get("nodes")
                .and_then(Value::as_array)
                .into_iter()
                .flatten()
        })
        .find(|node| {
            bool_field(node, "unlocked").unwrap_or(false)
                && !matches!(
                    str_field(node, "status").as_deref(),
                    Some("completed" | "tests_passed" | "submitted")
                )
        })
        .cloned()
}

fn write_json(path: &Path, value: &Value) -> Result<()> {
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent)?;
    }
    fs::write(path, serde_json::to_string_pretty(value)?)
        .with_context(|| format!("Escribiendo {}", path_string(path)))
}

fn set_json(target: &mut Value, key: &str, value: Value) {
    if !target.is_object() {
        *target = json!({});
    }
    target
        .as_object_mut()
        .unwrap()
        .insert(key.to_string(), value);
}

fn str_field(value: &Value, key: &str) -> Option<String> {
    value.get(key).and_then(|v| match v {
        Value::String(s) => Some(s.clone()),
        Value::Number(n) => Some(n.to_string()),
        Value::Bool(b) => Some(b.to_string()),
        _ => None,
    })
}

fn bool_field(value: &Value, key: &str) -> Option<bool> {
    value.get(key).and_then(|v| match v {
        Value::Bool(b) => Some(*b),
        Value::String(s) => s.parse().ok(),
        _ => None,
    })
}

fn bool_value(value: &Value, key: &str) -> bool {
    bool_field(value, key).unwrap_or(false)
}

fn int_value(value: &Value, key: &str) -> Option<i32> {
    value.get(key).and_then(|v| match v {
        Value::Number(n) => n.as_i64().map(|x| x as i32),
        Value::String(s) => s.parse().ok(),
        _ => None,
    })
}

fn string_param(value: &Value, key: &str) -> Option<String> {
    str_field(value, key)
        .or_else(|| str_field(value, &camel_param(key)))
        .filter(|value| !value.trim().is_empty())
}

fn camel_param(key: &str) -> String {
    let mut out = String::new();
    let mut upper = false;
    for ch in key.chars() {
        if ch == '-' || ch == '_' {
            upper = true;
        } else if upper {
            out.push(ch.to_ascii_uppercase());
            upper = false;
        } else {
            out.push(ch);
        }
    }
    out
}

fn path_string(path: &Path) -> String {
    path.to_string_lossy().to_string()
}

fn rel_string(path: &Path) -> String {
    path.components()
        .map(|c| c.as_os_str().to_string_lossy())
        .collect::<Vec<_>>()
        .join("\\")
}

fn now_iso() -> String {
    Utc::now().to_rfc3339_opts(SecondsFormat::Millis, true)
}

fn safe_file_name(value: &str) -> String {
    let invalid = Regex::new(r#"[<>:"/\\|?*]"#).unwrap();
    let spaces = Regex::new(r"\s+").unwrap();
    let name = spaces
        .replace_all(&invalid.replace_all(value, "-"), " ")
        .trim()
        .to_string();
    if name.is_empty() {
        "Ejercicio".to_string()
    } else {
        name
    }
}

fn to_slug(value: &str) -> String {
    let mut slug = String::new();
    let mut last_dash = false;
    for ch in value.to_ascii_lowercase().chars() {
        if ch.is_ascii_alphanumeric() {
            slug.push(ch);
            last_dash = false;
        } else if !last_dash {
            slug.push('-');
            last_dash = true;
        }
    }
    let slug = slug.trim_matches('-').to_string();
    if slug.is_empty() {
        "ejercicio".to_string()
    } else {
        slug
    }
}

fn file_name(path: &Path) -> String {
    path.file_name()
        .and_then(|s| s.to_str())
        .unwrap_or("ejercicio")
        .to_string()
}

fn replace_dir(path: &Path) -> Result<()> {
    if path.exists() {
        fs::remove_dir_all(path)?;
    }
    fs::create_dir_all(path)?;
    Ok(())
}

fn copy_dir_contents(source: &Path, target: &Path) -> Result<()> {
    fs::create_dir_all(target)?;
    for entry in fs::read_dir(source)? {
        let entry = entry?;
        let from = entry.path();
        let to = target.join(entry.file_name());
        if entry.file_type()?.is_dir() {
            copy_dir_contents(&from, &to)?;
        } else {
            if let Some(parent) = to.parent() {
                fs::create_dir_all(parent)?;
            }
            fs::copy(&from, &to)?;
        }
    }
    Ok(())
}

fn solution_files(root: &Path) -> Result<Vec<String>> {
    for config in [
        root.join(".exercism").join("config.json"),
        root.join(".estudio-exercism")
            .join("support")
            .join(".exercism")
            .join("config.json"),
    ] {
        if config.exists() {
            if let Ok(raw) = read_json(&config) {
                if let Some(files) = raw.pointer("/files/solution").and_then(Value::as_array) {
                    let result: Vec<String> = files
                        .iter()
                        .filter_map(Value::as_str)
                        .map(String::from)
                        .collect();
                    if !result.is_empty() {
                        return Ok(result);
                    }
                }
            }
        }
    }
    let metadata = root.join(".exercism").join("metadata.json");
    if metadata.exists() {
        if let Ok(raw) = read_json(&metadata) {
            if let Some(files) = raw.pointer("/files/solution").and_then(Value::as_array) {
                let result: Vec<String> = files
                    .iter()
                    .filter_map(Value::as_str)
                    .map(String::from)
                    .collect();
                if !result.is_empty() {
                    return Ok(result);
                }
            }
        }
    }
    let mut result = Vec::new();
    for entry in fs::read_dir(root)? {
        let entry = entry?;
        if entry.file_type()?.is_file() {
            let name = entry.file_name().to_string_lossy().to_string();
            let lower = name.to_ascii_lowercase();
            if lower.ends_with(".c") && !lower.contains("test") && !lower.contains("vendor") {
                result.push(name);
            }
        }
    }
    Ok(result)
}

fn meta_solution_files(meta: &Value) -> Option<Vec<String>> {
    let result: Vec<String> = meta
        .get("solutionFiles")
        .and_then(Value::as_array)?
        .iter()
        .filter_map(Value::as_str)
        .map(String::from)
        .collect();
    if result.is_empty() {
        None
    } else {
        Some(result)
    }
}

fn exercism_support_root(exercise_root: &Path, meta: &Value) -> PathBuf {
    if let Some(relative) = str_field(meta, "supportRoot") {
        let candidate = exercise_root.join(relative);
        if candidate.exists() {
            return candidate;
        }
    }
    let compact = exercise_root.join(".estudio-exercism").join("support");
    if compact.exists() {
        compact
    } else {
        exercise_root.to_path_buf()
    }
}

fn sync_solution_files(
    exercise_root: &Path,
    support_root: &Path,
    solution_files: &[String],
) -> Result<()> {
    for relative in solution_files {
        let source = exercise_root.join(relative);
        let destination = support_root.join(relative);
        if !source.exists() || source == destination {
            continue;
        }
        if let Some(parent) = destination.parent() {
            fs::create_dir_all(parent)?;
        }
        fs::copy(source, destination)?;
    }
    Ok(())
}

fn ensure_make_shim(root: &Path) -> Result<()> {
    let shim = root.join("make.cmd");
    if Path::new("C:\\msys64\\usr\\bin\\make.exe").exists() {
        if shim.exists() {
            let _ = fs::remove_file(shim);
        }
        return Ok(());
    }
    if find_in_path("make.exe").is_some() || find_in_path("make.cmd").is_some() {
        return Ok(());
    }
    let make = Path::new("C:\\msys64\\mingw64\\bin\\mingw32-make.exe");
    if make.exists() {
        fs::write(
            shim,
            format!("@echo off\r\n\"{}\" %*\r\n", path_string(make)),
        )?;
    }
    Ok(())
}

fn disable_test_ignore(root: &Path) -> Result<()> {
    fn visit(dir: &Path) -> Result<()> {
        for entry in fs::read_dir(dir)? {
            let entry = entry?;
            let path = entry.path();
            if entry.file_type()?.is_dir() {
                visit(&path)?;
            } else if entry.file_name().to_string_lossy().starts_with("test_")
                && path.extension().and_then(|e| e.to_str()) == Some("c")
            {
                let current = fs::read_to_string(&path)?;
                let updated = Regex::new(r"(?m)^(\s*)TEST_IGNORE\(\);")?
                    .replace_all(&current, "$1// TEST_IGNORE();")
                    .to_string();
                if updated != current {
                    fs::write(path, updated)?;
                }
            }
        }
        Ok(())
    }
    visit(root)
}

fn make_command(root: &Path) -> Result<PathBuf> {
    let shim = root.join("make.cmd");
    if shim.exists() {
        return Ok(shim);
    }
    for candidate in [
        find_in_path("make.exe"),
        Some(PathBuf::from("C:\\msys64\\usr\\bin\\make.exe")).filter(|path| path.exists()),
        Some(PathBuf::from("C:\\msys64\\mingw64\\bin\\mingw32-make.exe"))
            .filter(|path| path.exists()),
    ] {
        if let Some(path) = candidate {
            return Ok(path);
        }
    }
    Err(anyhow!(
        "No se encontro make para ejecutar los tests oficiales de Exercism."
    ))
}

fn imported_markers(dir: &Path) -> bool {
    [
        dir.join(".estudio-exercism.json"),
        dir.join(".exercism").join("metadata.json"),
        dir.join(".estudio-exercism").join("support"),
        dir.join(".estudio-exercism")
            .join("support")
            .join(".exercism")
            .join("metadata.json"),
        dir.join(".estudio-exercism")
            .join("support")
            .join(".exercism")
            .join("config.json"),
    ]
    .iter()
    .any(|path| path.exists())
}

fn template_file_name(exercise: &Value, provider: &str, slug: &str) -> String {
    if provider == "alejandro" {
        let base = str_field(exercise, "slug")
            .unwrap_or_else(|| slug.to_string())
            .trim_start_matches("alejandro-")
            .to_string();
        return format!("{}.c", to_slug(&base));
    }
    str_field(exercise, "fileName")
        .map(|name| safe_file_name(&name))
        .unwrap_or_else(|| "main.c".to_string())
}

fn select_instruction_markdown(markdown: &str) -> String {
    let clean = markdown.replace("\r\n", "\n");
    let mut cut = clean.len();
    for pattern in [
        r"(?im)^##\s+Fuente\b",
        r"(?im)^##\s+Source\b",
        r"(?im)^##\s+Credits?\b",
        r"(?im)^##\s+External\s+source\b",
    ] {
        if let Ok(regex) = Regex::new(pattern) {
            if let Some(found) = regex.find(&clean) {
                cut = cut.min(found.start());
            }
        }
    }
    clean[..cut].trim().to_string()
}

fn clear_translation_text(text: &str) -> String {
    let mut clean = text.trim().to_string();
    if let Ok(fence) = Regex::new(r"(?s)^```(?:markdown|md)?\s*(.*?)\s*```$") {
        if let Some(caps) = fence.captures(&clean) {
            clean = caps
                .get(1)
                .map(|m| m.as_str().trim().to_string())
                .unwrap_or(clean);
        }
    }
    let lines: Vec<&str> = clean.lines().collect();
    if let Some(index) = lines
        .iter()
        .position(|line| line.trim_start().starts_with("# "))
    {
        if index > 0 {
            let prefix = lines[..index].join(" ");
            if Regex::new(r"(?i)aqui tienes|traducci[oó]n|conservando|solicitad")
                .unwrap()
                .is_match(&prefix)
            {
                clean = lines[index..].join("\n").trim().to_string();
            }
        }
    }
    clean
}

fn c_comment_block(markdown: &str, title: &str, source: &str) -> String {
    let safe = markdown.replace("*/", "* /");
    let body = safe
        .lines()
        .filter(|line| !Regex::new(r"^\s*#+\s").unwrap().is_match(line))
        .collect::<Vec<_>>()
        .join("\n")
        .trim()
        .to_string();
    format!("/*\n{title}\n\nInstrucciones:\n{body}\n\nFuente: {source}\n*/\n\n\n")
}

fn add_header_to_solutions(
    root: &Path,
    files: &[String],
    readme: &str,
    title: &str,
    source: &str,
) -> Result<()> {
    let comment = c_comment_block(readme, title, source);
    for relative in files {
        if !relative.ends_with(".c") {
            continue;
        }
        let path = root.join(relative);
        if !path.exists() {
            continue;
        }
        let current = fs::read_to_string(&path)?;
        if current.contains("Estudio Socratico - instrucciones traducidas") {
            continue;
        }
        fs::write(path, format!("{comment}{current}"))?;
    }
    Ok(())
}

fn exercism_cli() -> Option<PathBuf> {
    find_in_path("exercism.exe")
        .or_else(|| find_in_path("exercism"))
        .or_else(|| {
            env::var("LOCALAPPDATA")
                .ok()
                .map(PathBuf::from)
                .map(|p| p.join("Microsoft").join("WindowsApps").join("exercism.exe"))
                .filter(|p| p.exists())
        })
}

fn exercism_workspace(cli: &Path) -> String {
    if let Ok(output) = std::process::Command::new(cli).arg("workspace").output() {
        let text = String::from_utf8_lossy(&output.stdout).trim().to_string();
        if !text.is_empty() {
            return text;
        }
    }
    env::var("USERPROFILE")
        .map(|p| PathBuf::from(p).join("Exercism"))
        .map(|p| path_string(&p))
        .unwrap_or_else(|_| "Exercism".to_string())
}

fn test_exercism_token(cli: &PathBuf) -> bool {
    if let Ok(output) = std::process::Command::new(cli)
        .args(["configure", "--show"])
        .output()
    {
        let text = format!(
            "{}{}",
            String::from_utf8_lossy(&output.stdout),
            String::from_utf8_lossy(&output.stderr)
        );
        for line in text.lines() {
            if line.trim_start().starts_with("Token:") {
                let lower = line.to_ascii_lowercase();
                return !lower.contains("not configured")
                    && line
                        .split(':')
                        .nth(1)
                        .map(|v| !v.trim().is_empty())
                        .unwrap_or(false);
            }
        }
    }
    exercism_token().is_some()
}

fn exercism_token() -> Option<String> {
    if let Ok(token) = env::var("EXERCISM_TOKEN") {
        if !token.trim().is_empty() {
            return Some(token.trim().to_string());
        }
    }
    let path = env::var("APPDATA")
        .ok()
        .map(PathBuf::from)?
        .join("exercism")
        .join("user.json");
    read_json(&path)
        .ok()
        .and_then(|value| str_field(&value, "token"))
        .filter(|token| !token.trim().is_empty())
}

fn solution_tests_status(solution: &Value) -> Option<String> {
    for key in [
        "published_iteration_head_tests_status",
        "latest_iteration_head_tests_status",
        "head_tests_status",
        "tests_status",
    ] {
        if let Some(value) = str_field(solution, key).filter(|value| !value.trim().is_empty()) {
            return Some(value);
        }
    }
    None
}

fn solution_url(solution: Option<&Value>, slug: &str, output: &[String]) -> String {
    for line in output {
        if let Some(found) = Regex::new(r"https?://\S+").unwrap().find(line) {
            return found
                .as_str()
                .trim_end_matches(['.', ',', ';', ')'])
                .to_string();
        }
    }
    if let Some(solution) = solution {
        if let Some(url) = str_field(solution, "private_url").filter(|s| !s.is_empty()) {
            return url;
        }
        if let Some(url) = str_field(solution, "public_url").filter(|s| !s.is_empty()) {
            return url;
        }
    }
    format!("https://exercism.org/tracks/c/exercises/{slug}")
}

async fn run_capture(
    cwd: Option<&Path>,
    program: &Path,
    args: &[&str],
    path_prefix: Option<&str>,
) -> Result<(i32, Vec<String>)> {
    let mut command = Command::new(program);
    command
        .args(args)
        .stdout(Stdio::piped())
        .stderr(Stdio::piped());
    if let Some(cwd) = cwd {
        command.current_dir(cwd);
    }
    if let Some(prefix) = path_prefix {
        let current = env::var_os("PATH").unwrap_or_else(OsString::new);
        command.env("PATH", format!("{prefix};{}", current.to_string_lossy()));
    }
    let output = command
        .output()
        .await
        .with_context(|| format!("Ejecutando {}", path_string(program)))?;
    let mut lines = Vec::new();
    lines.extend(
        String::from_utf8_lossy(&output.stdout)
            .lines()
            .map(String::from),
    );
    lines.extend(
        String::from_utf8_lossy(&output.stderr)
            .lines()
            .map(String::from),
    );
    Ok((output.status.code().unwrap_or(1), lines))
}

fn append_line(path: &Path, line: &str) -> Result<()> {
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent)?;
    }
    let mut file = fs::OpenOptions::new()
        .create(true)
        .append(true)
        .open(path)?;
    writeln!(file, "{line}")?;
    Ok(())
}

fn append_log(path: &Path, output: &[String], exit_code: i32) -> Result<()> {
    for line in output {
        append_line(path, line)?;
    }
    append_line(path, &format!("[EXIT CODE: {exit_code}]"))
}

fn log_c_files(log: &Path, exercise_root: &Path) -> Result<()> {
    append_line(log, "[ARCHIVOS C]")?;
    for entry in fs::read_dir(exercise_root)? {
        let entry = entry?;
        if entry.file_type()?.is_file()
            && entry.path().extension().and_then(|e| e.to_str()) == Some("c")
        {
            append_line(
                log,
                &format!("---- {} ----", entry.file_name().to_string_lossy()),
            )?;
            for line in fs::read_to_string(entry.path())?.lines() {
                append_line(log, line)?;
            }
        }
    }
    Ok(())
}

fn git_author(root: &Path, slug: &str) -> (String, String) {
    let get = |key: &str| -> Option<String> {
        std::process::Command::new("git")
            .args(["-C", &path_string(root), "config", "--local", "--get", key])
            .output()
            .ok()
            .map(|out| String::from_utf8_lossy(&out.stdout).trim().to_string())
            .filter(|value| !value.is_empty())
    };
    let github = get("github.user");
    let name = get("user.name")
        .or_else(|| github.clone())
        .unwrap_or_else(|| slug.to_string());
    let email = get("user.email").unwrap_or_else(|| {
        format!(
            "{}@users.noreply.github.com",
            github.unwrap_or_else(|| slug.to_string())
        )
    });
    (name, email)
}

fn rel_to(root: &Path, path: &Path) -> String {
    path.strip_prefix(root)
        .unwrap_or(path)
        .to_string_lossy()
        .trim_start_matches(['\\', '/'])
        .to_string()
}

fn find_in_path(name: &str) -> Option<PathBuf> {
    let paths = env::var_os("PATH")?;
    for dir in env::split_paths(&paths) {
        let candidate = dir.join(name);
        if candidate.exists() {
            return Some(candidate);
        }
    }
    None
}
