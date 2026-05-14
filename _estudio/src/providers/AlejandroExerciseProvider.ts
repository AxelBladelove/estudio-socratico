import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';

// Se importa el catálogo inyectado
import { ALEJANDRO_CATALOG } from '../generated/alejandro-catalog';

export class AlejandroExerciseProvider {
    private catalog: any;

    constructor() {
        this.catalog = ALEJANDRO_CATALOG;
    }

    public getExercises() {
        if (!this.catalog) {
            return [];
        }
        return this.catalog.exercises;
    }

    public async downloadAndCreateExercise(exerciseId: string, workspacePath: string) {
        if (!this.catalog) {
            vscode.window.showErrorMessage("El catálogo de Alejandro no está disponible.");
            return;
        }

        const exercise = this.catalog.exercises.find((ex: any) => ex.id === exerciseId);
        if (!exercise) {
            vscode.window.showErrorMessage(`Ejercicio ${exerciseId} no encontrado.`);
            return;
        }

        try {
            // 1. Crear carpeta local
            const exerciseDir = path.join(workspacePath, exercise.slug);
            if (!fs.existsSync(exerciseDir)) {
                fs.mkdirSync(exerciseDir, { recursive: true });
            }

            // 2. Descargar instructions.md desde el raw URL
            const response = await fetch(exercise.files.instructions);
            if (!response.ok) {
                throw new Error(`Failed to download instructions: ${response.statusText}`);
            }
            const instructions = await response.text();
            
            // 3. Crear archivo instructions.md local (opcional si se usa como Exercism)
            fs.writeFileSync(path.join(exerciseDir, 'instructions.md'), instructions);

            // 4. Crear un archivo .c con encabezado
            const initialCode = `/*
 * ${exercise.title}
 *
 * Instrucciones:
 * ${this.formatInstructionsForComment(instructions)}
 *
 * Fuente: Problemas de Programación — Rolando J. Batista & Alejandro J. Liz
 */

#include <stdio.h>

int main(void) {
    
    return 0;
}
`;
            const mainFile = path.join(exerciseDir, 'main.c');
            if (!fs.existsSync(mainFile)) {
                fs.writeFileSync(mainFile, initialCode);
            }

            // 5. Abrir el archivo
            const doc = await vscode.workspace.openTextDocument(mainFile);
            await vscode.window.showTextDocument(doc);
            
            vscode.window.showInformationMessage(`Ejercicio '${exercise.title}' inicializado con éxito.`);

        } catch (err: any) {
            vscode.window.showErrorMessage(`Error creando ejercicio: ${err.message}`);
        }
    }

    private formatInstructionsForComment(instructions: string): string {
        // Limpia el markdown básico para que se vea bien como un comentario de bloque en C
        let cleaned = instructions.replace(/^# .*\r?\n/gm, ''); // Quita el título h1
        cleaned = cleaned.replace(/^## .*\r?\n/gm, ''); // Quita subtítulos h2
        cleaned = cleaned.replace(/```text/g, '');
        cleaned = cleaned.replace(/```/g, '');
        
        // Aplica un prefijo de ' * ' a cada línea
        return cleaned.split(/\r?\n/).map(line => line.trim()).filter(line => line.length > 0).join('\n * ');
    }
}
