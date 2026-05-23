import test from "node:test";
import assert from "node:assert/strict";
import { getUpdateBannerView } from "./updateBannerState.js";

test("UpdaterBanner_ShownWhenNewReleaseExists", () => {
  const view = getUpdateBannerView({ status: "available", latestVersion: "2.0.15" }, "2.0.14");

  assert.equal(view.visible, true);
  assert.equal(view.title, "Actualización disponible");
  assert.equal(view.currentVersion, "v2.0.14");
  assert.equal(view.latestVersion, "v2.0.15");
  assert.equal(view.buttonLabel, "Actualizar ahora");
});

test("UpdaterBanner_HiddenWhenCurrentVersionIsLatest", () => {
  const view = getUpdateBannerView(null, "2.0.15");

  assert.equal(view.visible, false);
});

test("Updater_DoesNotBlockInitialScreenWhenNetworkFails", () => {
  const view = getUpdateBannerView({ status: "error", message: "offline", userTriggered: false }, "2.0.15");

  assert.equal(view.visible, false);
});
