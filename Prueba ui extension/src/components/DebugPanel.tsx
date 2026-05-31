import { useEffect, useMemo, useState } from "react";
import type { DebugState, ReferenceMode, ReferencePlacement } from "../App";
import {
  CALIBRATION_ELEMENT_IDS,
  CALIBRATION_LAYOUT,
  type CalibrationElement,
  type CalibrationElementId,
  type CalibrationOverrides,
} from "../lib/layout";

interface Props {
  debug: DebugState;
  onChange: (state: DebugState) => void;
  mouse: { x: number; y: number };
  pin: { x: number; y: number } | null;
  scale: number;
  pathD: string;
  pathConfigJson: string;
  layoutJson: string;
  allCalibrationJson: string;
  selectedElementId: CalibrationElementId;
  onSelectedElementIdChange: (id: CalibrationElementId) => void;
  calibrationOverrides: CalibrationOverrides;
  onPatchCalibrationElement: (id: CalibrationElementId, patch: Partial<CalibrationElement>) => void;
  onResetCalibrationElement: (id: CalibrationElementId) => void;
  hiddenCalibrationIds: CalibrationElementId[];
  onHiddenCalibrationIdsChange: (ids: CalibrationElementId[]) => void;
}

export default function DebugPanel({
  debug,
  onChange,
  mouse,
  pin,
  scale,
  pathD,
  pathConfigJson,
  layoutJson,
  allCalibrationJson,
  selectedElementId,
  onSelectedElementIdChange,
  calibrationOverrides,
  onPatchCalibrationElement,
  onResetCalibrationElement,
  hiddenCalibrationIds,
  onHiddenCalibrationIdsChange,
}: Props) {
  const [hidden, setHidden] = useState(false);
  const [measurement, setMeasurement] = useState<{ x: number; y: number; w: number; h: number; measuredWidth?: number } | null>(null);
  const patch = (next: Partial<DebugState>) => onChange({ ...debug, ...next });
  const selectedBase = CALIBRATION_LAYOUT.elements[selectedElementId];
  const selected = useMemo(
    () => ({ ...selectedBase, ...calibrationOverrides[selectedElementId] }),
    [calibrationOverrides, selectedBase, selectedElementId],
  );
  const resetDebugView = () =>
    patch({
      sideBySide: false,
      showReference: true,
      referencePlacement: "over",
      referenceOpacity: 0.5,
      referenceMode: "normal",
      showGrid: false,
      showHotspots: false,
      showUi: true,
      showPathPoints: false,
      hideTextCheckDistractors: false,
      calibrationMode: false,
      calibrationView: "all",
      pathCalibrationMode: false,
      useGeneratedPath: false,
      useGeneratedPathV2: true,
      showRawV2Path: false,
      showExtractedPoints: false,
      showSkeletonOverlay: false,
      showMaskOverlay: false,
      showPathHandles: false,
    });
  const copyText = async (text: string) => {
    await navigator.clipboard.writeText(text);
    console.log(`[debug] copied: ${text}`);
  };
  const enterCalibration = (mode: DebugState["calibrationView"] = "all", difference = false) =>
    patch({
      sideBySide: false,
      showReference: true,
      showUi: true,
      referenceOpacity: 0.5,
      referenceMode: difference ? "difference" : "normal",
      referencePlacement: "over",
      showHotspots: false,
      showGrid: false,
      showPathPoints: false,
      hideTextCheckDistractors: false,
      calibrationMode: true,
      calibrationView: mode,
      pathCalibrationMode: false,
      useGeneratedPath: false,
      useGeneratedPathV2: true,
      showRawV2Path: false,
      showExtractedPoints: false,
      showSkeletonOverlay: false,
      showMaskOverlay: false,
      showPathHandles: false,
    });
  const enterPathCalibration = (difference = false) =>
    patch({
      sideBySide: false,
      showReference: true,
      showUi: true,
      referenceOpacity: 0.5,
      referenceMode: difference ? "difference" : "normal",
      referencePlacement: "over",
      showHotspots: false,
      showGrid: false,
      showPathPoints: true,
      hideTextCheckDistractors: false,
      calibrationMode: false,
      calibrationView: "all",
      pathCalibrationMode: true,
      useGeneratedPath: false,
      useGeneratedPathV2: false,
      showRawV2Path: false,
      showPathHandles: true,
    });

  const nudge = (field: keyof CalibrationElement, delta: number) => {
    const current = Number(selected[field] ?? (field === "scaleX" || field === "scaleY" ? 1 : 0));
    onPatchCalibrationElement(selectedElementId, { [field]: Number((current + delta).toFixed(3)) } as Partial<CalibrationElement>);
  };

  const setField = (field: keyof CalibrationElement, value: string) => {
    if (field === "fontFamily") {
      onPatchCalibrationElement(selectedElementId, { fontFamily: value });
      return;
    }
    onPatchCalibrationElement(selectedElementId, { [field]: Number(value) } as Partial<CalibrationElement>);
  };

  const toggleHiddenSelected = () => {
    onHiddenCalibrationIdsChange(
      hiddenCalibrationIds.includes(selectedElementId)
        ? hiddenCalibrationIds.filter((id) => id !== selectedElementId)
        : [...hiddenCalibrationIds, selectedElementId],
    );
  };

  useEffect(() => {
    if (!debug.calibrationMode) {
      return;
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (!["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown"].includes(event.key)) {
        return;
      }
      event.preventDefault();
      const sign = event.key === "ArrowLeft" || event.key === "ArrowUp" ? -1 : 1;
      const isHorizontal = event.key === "ArrowLeft" || event.key === "ArrowRight";
      if (event.ctrlKey && event.altKey && isHorizontal) {
        nudge("rotate", sign);
        return;
      }
      if (event.altKey) {
        nudge(isHorizontal ? "scaleX" : "scaleY", sign * 0.01);
        return;
      }
      if (event.ctrlKey) {
        nudge(isHorizontal ? "w" : "h", sign);
        return;
      }
      const amount = event.shiftKey ? 0.5 : 1;
      nudge(isHorizontal ? "x" : "y", sign * amount);
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  });

  useEffect(() => {
    const updateMeasurement = () => {
      const stage = document.querySelector(".stage")?.getBoundingClientRect();
      const element = document.querySelector(`[data-cal-id="${CSS.escape(selectedElementId)}"]`)?.getBoundingClientRect();
      if (!stage || !element) {
        setMeasurement(null);
        return;
      }
      const scaleX = 1086 / stage.width;
      const scaleY = 1448 / stage.height;
      const next = {
        x: Number(((element.left - stage.left) * scaleX).toFixed(1)),
        y: Number(((element.top - stage.top) * scaleY).toFixed(1)),
        w: Number((element.width * scaleX).toFixed(1)),
        h: Number((element.height * scaleY).toFixed(1)),
      };
      if (selected.type === "text") {
        const canvas = document.createElement("canvas");
        const context = canvas.getContext("2d");
        if (context) {
          context.font = `${selected.fontWeight ?? 400} ${selected.fontSize ?? 16}px ${selected.fontFamily ?? "Inter"}`;
          const text = document.querySelector(`[data-cal-id="${CSS.escape(selectedElementId)}"]`)?.textContent ?? "";
          setMeasurement({ ...next, measuredWidth: Number(context.measureText(text).width.toFixed(1)) });
          return;
        }
      }
      setMeasurement(next);
    };
    updateMeasurement();
    const id = window.setInterval(updateMeasurement, 300);
    return () => window.clearInterval(id);
  }, [selected, selectedElementId]);

  if (hidden) {
    return null;
  }

  return (
    <aside className="debug-panel" aria-label="Panel de debug">
      <div className="debug-title">
        Debug
        <button type="button" onClick={() => setHidden(true)} aria-label="Ocultar panel de debug">
          Ocultar
        </button>
      </div>
      <div className="quick-actions">
        <button
          type="button"
          onClick={() =>
            patch({
              sideBySide: false,
              showReference: true,
              showUi: false,
              referenceOpacity: 1,
              referenceMode: "normal",
              showHotspots: false,
              showPathPoints: false,
              hideTextCheckDistractors: false,
              calibrationMode: false,
              calibrationView: "all",
              pathCalibrationMode: false,
              useGeneratedPath: false,
              useGeneratedPathV2: true,
              showRawV2Path: false,
            })
          }
        >
          Solo referencia
        </button>
        <button
          type="button"
          onClick={() =>
            patch({
              sideBySide: false,
              showReference: false,
              showUi: true,
              referenceMode: "normal",
              showHotspots: false,
              showPathPoints: false,
              hideTextCheckDistractors: false,
              calibrationMode: false,
              calibrationView: "all",
              pathCalibrationMode: false,
              useGeneratedPath: false,
              useGeneratedPathV2: true,
              showRawV2Path: false,
            })
          }
        >
          Solo UI
        </button>
        <button
          type="button"
          onClick={() =>
            patch({
              sideBySide: false,
              showReference: true,
              showUi: true,
              referenceOpacity: 0.5,
              referenceMode: "normal",
              referencePlacement: "over",
              showHotspots: false,
              showPathPoints: false,
              hideTextCheckDistractors: false,
              calibrationMode: false,
              calibrationView: "all",
              pathCalibrationMode: false,
              useGeneratedPath: false,
              useGeneratedPathV2: true,
              showRawV2Path: false,
            })
          }
        >
          Overlay 50%
        </button>
        <button
          type="button"
          onClick={() =>
            patch({
              sideBySide: false,
              showReference: true,
              showUi: true,
              referenceOpacity: 0.5,
              referenceMode: "difference",
              referencePlacement: "over",
              showHotspots: false,
              showPathPoints: false,
              hideTextCheckDistractors: false,
              calibrationMode: false,
              calibrationView: "all",
              pathCalibrationMode: false,
              useGeneratedPath: false,
              useGeneratedPathV2: true,
              showRawV2Path: false,
            })
          }
        >
          Difference 50%
        </button>
        <button
          type="button"
          onClick={() =>
            patch({
              sideBySide: true,
              showReference: false,
              showUi: true,
              referenceOpacity: 0,
              referenceMode: "normal",
              referencePlacement: "over",
              showHotspots: false,
              showGrid: false,
              showPathPoints: false,
              hideTextCheckDistractors: false,
              calibrationMode: false,
              calibrationView: "all",
              pathCalibrationMode: false,
              useGeneratedPath: false,
              useGeneratedPathV2: true,
              showRawV2Path: false,
              showExtractedPoints: false,
              showSkeletonOverlay: false,
              showMaskOverlay: false,
              showPathHandles: false,
            })
          }
        >
          Side by side
        </button>
        <button
          type="button"
          onClick={() => enterCalibration("all", false)}
        >
          Element Calibration
        </button>
        <button
          type="button"
          onClick={() => enterCalibration("all", true)}
        >
          Difference Text Check
        </button>
        <button type="button" onClick={() => enterCalibration("selected", false)}>
          Selected Element Only
        </button>
        <button type="button" onClick={() => enterCalibration("text", false)}>
          Text Only
        </button>
        <button type="button" onClick={() => enterCalibration("icons", false)}>
          Icons Only
        </button>
        <button type="button" onClick={() => enterCalibration("progress", false)}>
          Progress Only
        </button>
        <button type="button" onClick={() => enterCalibration("nodes", false)}>
          Nodes Only
        </button>
        <button type="button" onClick={() => enterPathCalibration(false)}>
          Path Calibration
        </button>
        <button type="button" onClick={() => enterPathCalibration(true)}>
          Path Difference 50%
        </button>
        <button
          type="button"
          onClick={() =>
            patch({
              showReference: true,
              showUi: true,
              referenceOpacity: 0.5,
              referenceMode: "normal",
              referencePlacement: "over",
              showHotspots: false,
              showGrid: false,
              showPathPoints: false,
              hideTextCheckDistractors: false,
              calibrationMode: false,
              calibrationView: "all",
              pathCalibrationMode: false,
              useGeneratedPath: true,
              useGeneratedPathV2: false,
              showRawV2Path: false,
              showExtractedPoints: true,
              showSkeletonOverlay: true,
              showMaskOverlay: true,
              showPathHandles: false,
            })
          }
        >
          Snake Debug All
        </button>
        <button
          type="button"
          onClick={() =>
            patch({
              showReference: true,
              showUi: true,
              referenceOpacity: 0.5,
              referenceMode: "normal",
              referencePlacement: "over",
              showHotspots: false,
              showGrid: false,
              showPathPoints: false,
              hideTextCheckDistractors: false,
              calibrationMode: false,
              calibrationView: "all",
              pathCalibrationMode: false,
              useGeneratedPath: false,
              useGeneratedPathV2: true,
              showRawV2Path: false,
              showExtractedPoints: true,
              showSkeletonOverlay: true,
              showMaskOverlay: true,
              showPathHandles: false,
            })
          }
        >
          Snake V2 Debug All
        </button>
        <button
          type="button"
          onClick={() =>
            patch({
              showReference: true,
              showUi: true,
              referenceOpacity: 0.5,
              referenceMode: "normal",
              referencePlacement: "over",
              showHotspots: false,
              showGrid: false,
              showPathPoints: false,
              hideTextCheckDistractors: false,
              calibrationMode: false,
              calibrationView: "all",
              pathCalibrationMode: false,
              useGeneratedPath: true,
              useGeneratedPathV2: false,
              showRawV2Path: false,
              showExtractedPoints: false,
              showSkeletonOverlay: false,
              showMaskOverlay: false,
              showPathHandles: false,
            })
          }
        >
          Generated Path Only
        </button>
        <button
          type="button"
          onClick={() =>
            patch({
              showReference: true,
              showUi: true,
              referenceOpacity: 0.5,
              referenceMode: "normal",
              referencePlacement: "over",
              showHotspots: false,
              showGrid: false,
              showPathPoints: false,
              hideTextCheckDistractors: false,
              calibrationMode: false,
              calibrationView: "all",
              pathCalibrationMode: false,
              useGeneratedPath: false,
              useGeneratedPathV2: true,
              showRawV2Path: false,
              showExtractedPoints: false,
              showSkeletonOverlay: false,
              showMaskOverlay: false,
              showPathHandles: false,
            })
          }
        >
          Generated Path V2 Only
        </button>
        <button
          type="button"
          onClick={() =>
            patch({
              showReference: true,
              showUi: true,
              referenceOpacity: 0.5,
              referenceMode: "normal",
              referencePlacement: "over",
              showHotspots: false,
              showGrid: false,
              showPathPoints: false,
              hideTextCheckDistractors: false,
              calibrationMode: false,
              calibrationView: "all",
              pathCalibrationMode: false,
              useGeneratedPath: false,
              useGeneratedPathV2: false,
              showRawV2Path: false,
              showExtractedPoints: false,
              showSkeletonOverlay: false,
              showMaskOverlay: false,
              showPathHandles: false,
            })
          }
        >
          Manual Path Only
        </button>
      </div>
      <section className="calibration-panel">
        <div className="calibration-heading">Element Calibration</div>
        <select value={selectedElementId} onChange={(event) => onSelectedElementIdChange(event.currentTarget.value as CalibrationElementId)}>
          {CALIBRATION_ELEMENT_IDS.map((id) => (
            <option key={id} value={id}>
              {id}
            </option>
          ))}
        </select>
        <div className="calibration-type">{selected.type}</div>
        <div className="calibration-grid">
          {(["x", "y", "w", "h", "fontSize", "lineHeight", "fontWeight", "letterSpacing", "scaleX", "scaleY", "rotate", "fontFamily"] as Array<
            keyof CalibrationElement
          >).map((field) => (
            <label key={field}>
              {field}
              <input
                value={(selected[field] as string | number | undefined) ?? ""}
                onChange={(event) => setField(field, event.currentTarget.value)}
                type={field === "fontFamily" ? "text" : "number"}
                step={field === "scaleX" || field === "scaleY" ? "0.01" : "0.5"}
              />
            </label>
          ))}
        </div>
        <div className="nudge-grid">
          <button type="button" onClick={() => nudge("x", -1)}>x -1</button>
          <button type="button" onClick={() => nudge("x", 1)}>x +1</button>
          <button type="button" onClick={() => nudge("y", -1)}>y -1</button>
          <button type="button" onClick={() => nudge("y", 1)}>y +1</button>
          <button type="button" onClick={() => nudge("x", -0.5)}>x -0.5</button>
          <button type="button" onClick={() => nudge("x", 0.5)}>x +0.5</button>
          <button type="button" onClick={() => nudge("y", -0.5)}>y -0.5</button>
          <button type="button" onClick={() => nudge("y", 0.5)}>y +0.5</button>
          <button type="button" onClick={() => nudge("w", -1)}>w -1</button>
          <button type="button" onClick={() => nudge("w", 1)}>w +1</button>
          <button type="button" onClick={() => nudge("h", -1)}>h -1</button>
          <button type="button" onClick={() => nudge("h", 1)}>h +1</button>
          <button type="button" onClick={() => nudge("scaleX", -0.01)}>scaleX -0.01</button>
          <button type="button" onClick={() => nudge("scaleX", 0.01)}>scaleX +0.01</button>
          <button type="button" onClick={() => nudge("scaleY", -0.01)}>scaleY -0.01</button>
          <button type="button" onClick={() => nudge("scaleY", 0.01)}>scaleY +0.01</button>
          <button type="button" onClick={() => nudge("rotate", -1)}>rotate -1</button>
          <button type="button" onClick={() => nudge("rotate", 1)}>rotate +1</button>
        </div>
        <div className="quick-actions">
          <button type="button" onClick={() => copyText(JSON.stringify(selected, null, 2))}>
            copy selected config
          </button>
          <button type="button" onClick={() => copyText(allCalibrationJson)}>
            copy all layout JSON
          </button>
          <button type="button" onClick={() => onResetCalibrationElement(selectedElementId)}>
            reset selected element
          </button>
          <button type="button" onClick={() => enterCalibration("selected", false)}>
            solo selected element
          </button>
          <button type="button" onClick={toggleHiddenSelected}>
            hide selected element
          </button>
          <button type="button" disabled>
            Auto fit selected element
          </button>
        </div>
        <dl className="measurement-list">
          <div><dt>x/y</dt><dd>{measurement ? `${measurement.x}, ${measurement.y}` : "-"}</dd></div>
          <div><dt>w/h</dt><dd>{measurement ? `${measurement.w}, ${measurement.h}` : "-"}</dd></div>
          <div><dt>text w</dt><dd>{measurement?.measuredWidth ?? "-"}</dd></div>
        </dl>
      </section>
      <label>
        <input
          type="checkbox"
          checked={debug.showReference}
          onChange={(event) => patch({ showReference: event.currentTarget.checked })}
        />
        Referencia
      </label>
      <div className="segmented">
        <button
          type="button"
          className={debug.referencePlacement === "under" ? "active" : ""}
          onClick={() => patch({ referencePlacement: "under" as ReferencePlacement })}
        >
          Debajo
        </button>
        <button
          type="button"
          className={debug.referencePlacement === "over" ? "active" : ""}
          onClick={() => patch({ referencePlacement: "over" as ReferencePlacement })}
        >
          Encima
        </button>
      </div>
      <label className="range-row">
        Opacidad
        <input
          type="range"
          min="0"
          max="1"
          step="0.01"
          value={debug.referenceOpacity}
          onChange={(event) => patch({ referenceOpacity: Number(event.currentTarget.value) })}
        />
        <span>{Math.round(debug.referenceOpacity * 100)}%</span>
      </label>
      <div className="segmented">
        <button
          type="button"
          className={debug.referenceMode === "normal" ? "active" : ""}
          onClick={() => patch({ referenceMode: "normal" as ReferenceMode })}
        >
          Normal
        </button>
        <button
          type="button"
          className={debug.referenceMode === "difference" ? "active" : ""}
          onClick={() => patch({ referenceMode: "difference" as ReferenceMode })}
        >
          Difference
        </button>
      </div>
      <label>
        <input type="checkbox" checked={debug.showGrid} onChange={(event) => patch({ showGrid: event.currentTarget.checked })} />
        Grid
      </label>
      <label>
        <input
          type="checkbox"
          checked={debug.showHotspots}
          onChange={(event) => patch({ showHotspots: event.currentTarget.checked })}
        />
        Hotspots
      </label>
      <label>
        <input type="checkbox" checked={debug.showUi} onChange={(event) => patch({ showUi: event.currentTarget.checked })} />
        UI
      </label>
      <label>
        <input
          type="checkbox"
          checked={debug.showPathPoints}
          onChange={(event) => patch({ showPathPoints: event.currentTarget.checked })}
        />
        Path edit
      </label>
      <label>
        <input
          type="checkbox"
          checked={debug.useGeneratedPath}
          onChange={(event) =>
            patch({
              useGeneratedPath: event.currentTarget.checked,
              useGeneratedPathV2: event.currentTarget.checked ? false : debug.useGeneratedPathV2,
            })
          }
        />
        Use generated path
      </label>
      <label>
        <input
          type="checkbox"
          checked={debug.useGeneratedPathV2}
          onChange={(event) =>
            patch({
              useGeneratedPathV2: event.currentTarget.checked,
              useGeneratedPath: event.currentTarget.checked ? false : debug.useGeneratedPath,
            })
          }
        />
        Use generated path v2 (Spline)
      </label>
      <label>
        <input
          type="checkbox"
          checked={debug.showExtractedPoints}
          onChange={(event) => patch({ showExtractedPoints: event.currentTarget.checked })}
        />
        Show extracted points
      </label>
      <label>
        <input
          type="checkbox"
          checked={debug.showSkeletonOverlay}
          onChange={(event) => patch({ showSkeletonOverlay: event.currentTarget.checked })}
        />
        Show skeleton overlay
      </label>
      <label>
        <input
          type="checkbox"
          checked={debug.showMaskOverlay}
          onChange={(event) => patch({ showMaskOverlay: event.currentTarget.checked })}
        />
        Show mask overlay
      </label>
      <label>
        <input
          type="checkbox"
          checked={debug.showPathHandles}
          onChange={(event) => patch({ showPathHandles: event.currentTarget.checked })}
        />
        Show path handles
      </label>
      <label>
        <input
          type="checkbox"
          checked={debug.showRawV2Path}
          onChange={(event) => patch({ showRawV2Path: event.currentTarget.checked })}
        />
        Compare: Path V2 vs Smoothed
      </label>
      <div className="quick-actions">
        <button type="button" onClick={() => copyText(`${mouse.x}, ${mouse.y}`)}>
          Copiar coordenada actual
        </button>
        <button type="button" onClick={() => copyText(layoutJson)}>
          Copiar layout JSON
        </button>
        <button type="button" onClick={() => copyText(pathD)}>
          Copiar path d
        </button>
        <button type="button" onClick={() => copyText(pathConfigJson)}>
          Copy path config
        </button>
        <button type="button" onClick={resetDebugView}>
          Reset debug view
        </button>
      </div>
      <dl>
        <div>
          <dt>Escala</dt>
          <dd>{scale.toFixed(3)}</dd>
        </div>
        <div>
          <dt>Mouse</dt>
          <dd>
            {mouse.x}, {mouse.y}
          </dd>
        </div>
        <div>
          <dt>ALT</dt>
          <dd>{pin ? `${pin.x}, ${pin.y}` : "-"}</dd>
        </div>
      </dl>
    </aside>
  );
}
