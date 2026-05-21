import test from "node:test";
import assert from "node:assert/strict";
import { summaryIsReady } from "./stateMapping.js";
import { finalIssues, finalTitle } from "./finalState.js";

test("FinalState_AllReadyUiShowsSuccess", () => {
  const summary = {
    succeeded: true,
    currentState: {
      globalState: "readyToStudy",
      finalReadiness: {
        missingRequirements: [],
        failedRequirements: [],
        authRequirements: [],
        smokeTestStatus: "passed",
      },
    },
  };

  assert.equal(summaryIsReady("setup", summary, null), true);
  assert.equal(finalTitle("setup", true, summary, null), "Todo está listo para estudiar C");
});

test("IncompleteResult_IncludesMissingRequirement", () => {
  const summary = {
    succeeded: true,
    currentState: {
      globalState: "needsUserAction",
      finalReadiness: {
        missingRequirements: ["Workspace"],
        failedRequirements: [],
        authRequirements: [],
        smokeTestStatus: "pending",
      },
    },
  };

  assert.deepEqual(finalIssues("setup", summary, null), [
    "Falta: Workspace",
    "Smoke test F9: pendiente",
  ]);
});
