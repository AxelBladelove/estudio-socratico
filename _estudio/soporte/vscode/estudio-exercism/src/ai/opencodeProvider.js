const cp = require("child_process");

class OpenCodeProvider {
  constructor(config, execFile = cp.execFile) {
    this.name = "opencode";
    this.config = config || {};
    this.execFile = execFile;
    this.command = String(this.config.experimental?.opencodeCommand || "opencode").trim() || "opencode";
  }

  async isAvailable() {
    if (!this.config.features?.opencodeIntegration) {
      return { available: false, status: "requiresConfiguration", reason: "opencodeIntegration esta apagado." };
    }

    const version = await execFileSafe(this.execFile, this.command, ["--version"], { timeout: 5000 });
    if (version.error) {
      return { available: false, status: "notInstalled", reason: "No se pudo ejecutar opencode." };
    }

    return {
      available: true,
      status: "ready",
      version: firstLine(version.stdout || version.stderr),
      managed: false,
    };
  }

  async listModels() {
    const availability = await this.isAvailable();
    if (!availability.available) return [];

    const result = await execFileSafe(this.execFile, this.command, ["models", "--json"], { timeout: 10000 });
    if (result.error) return [];

    try {
      const parsed = JSON.parse(result.stdout);
      return Array.isArray(parsed) ? parsed : parsed.models || [];
    } catch {
      return [];
    }
  }

  async complete(prompt, options = {}) {
    const availability = await this.isAvailable();
    if (!availability.available) {
      return { ok: false, unavailable: true, provider: this.name, reason: availability.reason };
    }

    const args = ["run", "--prompt", prompt];
    if (options.model) {
      args.push("--model", options.model);
    }

    const result = await execFileSafe(this.execFile, this.command, args, {
      timeout: options.timeoutMs || 120000,
      maxBuffer: 1024 * 1024 * 4,
    });

    if (result.error) {
      return { ok: false, provider: this.name, model: options.model || "", reason: "OpenCode no pudo completar el analisis." };
    }

    return { ok: true, provider: this.name, model: options.model || "", text: result.stdout.trim() };
  }
}

function execFileSafe(execFile, file, args, options) {
  return new Promise((resolve) => {
    execFile(file, args, options, (error, stdout, stderr) => {
      resolve({ error, stdout: stdout || "", stderr: stderr || "" });
    });
  });
}

function firstLine(value) {
  return String(value || "").split(/\r?\n/).find(Boolean) || "";
}

module.exports = {
  OpenCodeProvider,
};
