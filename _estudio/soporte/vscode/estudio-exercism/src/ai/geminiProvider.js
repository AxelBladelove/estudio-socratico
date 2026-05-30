class GeminiProvider {
  constructor(config, fetchImpl = globalThis.fetch) {
    this.name = "gemini";
    this.config = config || {};
    this.fetch = fetchImpl;
  }

  async isAvailable() {
    if (String(this.config.provider || "gemini").toLowerCase() !== "gemini") {
      return { available: false, status: "requiresConfiguration", reason: "El provider principal no es gemini." };
    }
    if (!this.getApiKey()) {
      return { available: false, status: "requiresConfiguration", reason: "Falta apiKey local para Gemini." };
    }
    if (typeof this.fetch !== "function") {
      return { available: false, status: "error", reason: "fetch no esta disponible en este runtime." };
    }
    return { available: true, status: "ready", version: this.config.model || "gemini-2.5-flash" };
  }

  async complete(prompt, options = {}) {
    const availability = await this.isAvailable();
    if (!availability.available) {
      return { ok: false, unavailable: true, provider: this.name, reason: availability.reason };
    }

    const model = String(options.model || this.config.model || "gemini-2.5-flash");
    const apiKey = this.getApiKey();
    const endpoint = `https://generativelanguage.googleapis.com/v1beta/models/${encodeURIComponent(model)}:generateContent?key=${encodeURIComponent(apiKey)}`;
    const response = await this.fetch(endpoint, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        contents: [{ role: "user", parts: [{ text: prompt }] }],
        generationConfig: {
          temperature: options.temperature ?? 0.2,
          responseMimeType: "application/json",
        },
      }),
    });

    const bodyText = await response.text();
    if (!response.ok) {
      return {
        ok: false,
        provider: this.name,
        model,
        reason: `Gemini respondio ${response.status}.`,
      };
    }

    let json;
    try {
      json = JSON.parse(bodyText);
    } catch {
      return { ok: false, provider: this.name, model, reason: "Gemini devolvio JSON invalido." };
    }

    const text = json?.candidates?.[0]?.content?.parts?.map((part) => part.text || "").join("\n").trim() || "";
    return { ok: true, provider: this.name, model, text };
  }

  getApiKey() {
    return String(this.config.apiKey || "").trim() || String(process.env.GEMINI_API_KEY || "").trim();
  }
}

module.exports = {
  GeminiProvider,
};
