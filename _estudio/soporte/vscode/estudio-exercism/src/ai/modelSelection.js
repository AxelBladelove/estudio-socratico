const C_EXERCISE_MODEL_PRIORITIES = [
  "gemini-2.5-flash",
  "claude-3-5-sonnet",
  "gpt-4.1",
  "qwen2.5-coder",
  "deepseek-coder",
];

function selectModel(strategy, models, fallbackModel) {
  if (strategy !== "best-for-c-exercises" || !Array.isArray(models) || models.length === 0) {
    return fallbackModel || "";
  }

  const normalized = models.map((model) => String(model?.id || model?.name || model));
  for (const candidate of C_EXERCISE_MODEL_PRIORITIES) {
    const match = normalized.find((model) => model.toLowerCase().includes(candidate.toLowerCase()));
    if (match) return match;
  }

  return normalized[0] || fallbackModel || "";
}

module.exports = {
  selectModel,
};
