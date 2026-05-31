import { useEffect, useMemo, useState, type CSSProperties } from "react";
import DebugPanel from "./components/DebugPanel";
import Stage from "./components/Stage";
import {
  CALIBRATION_ELEMENT_IDS,
  CALIBRATION_LAYOUT,
  LAYOUT,
  STAGE,
  useStageScale,
  type CalibrationElementId,
  type CalibrationOverrides,
} from "./lib/layout";
import { buildSnakePaths, GENERATED_SNAKE_V2_PATH_D, loadPathPoints, savePathPoints, type PathPoint } from "./lib/snake";
import { GENERATED_SNAKE_PATH_D } from "./lib/snakePath.generated";

export type ReferenceMode = "normal" | "difference";
export type ReferencePlacement = "under" | "over";

export interface DebugState {
  sideBySide: boolean;
  showReference: boolean;
  referencePlacement: ReferencePlacement;
  referenceOpacity: number;
  referenceMode: ReferenceMode;
  showGrid: boolean;
  showHotspots: boolean;
  showUi: boolean;
  showPathPoints: boolean;
  hideTextCheckDistractors: boolean;
  calibrationMode: boolean;
  calibrationView: "all" | "selected" | "text" | "icons" | "progress" | "nodes";
  pathCalibrationMode: boolean;
  useGeneratedPath: boolean;
  useGeneratedPathV2: boolean;
  showRawV2Path: boolean;
  showExtractedPoints: boolean;
  showSkeletonOverlay: boolean;
  showMaskOverlay: boolean;
  showPathHandles: boolean;
}

export default function App() {
  const [mouse, setMouse] = useState({ x: 0, y: 0 });
  const [pin, setPin] = useState<{ x: number; y: number } | null>(null);
  const [pathPoints, setPathPoints] = useState<PathPoint[]>(() => loadPathPoints());
  const [selectedElementId, setSelectedElementId] = useState<CalibrationElementId>("mainTitle.text");
  const [calibrationOverrides, setCalibrationOverrides] = useState<CalibrationOverrides>(() => {
    try {
      const raw = window.localStorage.getItem("element-calibration-overrides");
      const parsed = raw ? (JSON.parse(raw) as CalibrationOverrides) : {};
      return Object.fromEntries(
        Object.entries(parsed).map(([id, override]) => [
          id,
          {
            ...override,
            fontFamily:
              override?.fontFamily === "Inter"
                ? "var(--font)"
                : override?.fontFamily === "Cascadia Code"
                  ? "var(--mono)"
                  : override?.fontFamily,
          },
        ]),
      ) as CalibrationOverrides;
    } catch {
      return {};
    }
  });
  const [hiddenCalibrationIds, setHiddenCalibrationIds] = useState<CalibrationElementId[]>([]);
  const [debug, setDebug] = useState<DebugState>({
    sideBySide: false,
    showReference: true,
    referencePlacement: "over",
    referenceOpacity: 0.48,
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
  const scale = useStageScale(debug.sideBySide ? 2 : 1);

  useEffect(() => {
    savePathPoints(pathPoints);
  }, [pathPoints]);

  useEffect(() => {
    window.localStorage.setItem("element-calibration-overrides", JSON.stringify(calibrationOverrides));
  }, [calibrationOverrides]);

  const patchCalibrationElement = (id: CalibrationElementId, patch: Partial<(typeof CALIBRATION_LAYOUT.elements)[CalibrationElementId]>) => {
    setCalibrationOverrides((current) => ({
      ...current,
      [id]: {
        ...current[id],
        ...patch,
      },
    }));
  };

  const resetCalibrationElement = (id: CalibrationElementId) => {
    setCalibrationOverrides((current) => {
      const next = { ...current };
      delete next[id];
      return next;
    });
  };

  const allCalibrationJson = useMemo(
    () =>
      JSON.stringify(
        {
          elements: Object.fromEntries(
            CALIBRATION_ELEMENT_IDS.map((id) => [id, { ...CALIBRATION_LAYOUT.elements[id], ...calibrationOverrides[id] }]),
          ),
        },
        null,
        2,
      ),
    [calibrationOverrides],
  );
  const pathConfigJson = useMemo(
    () =>
      JSON.stringify(
        {
          points: pathPoints,
          paths: buildSnakePaths(pathPoints),
          generatedPathD: GENERATED_SNAKE_PATH_D,
          generatedV2PathD: GENERATED_SNAKE_V2_PATH_D,
        },
        null,
        2,
      ),
    [pathPoints],
  );

  const styleVars = useMemo(
    () =>
      ({
        "--stage-scale": scale,
      }) as CSSProperties,
    [scale],
  );

  return (
    <div className={`app ${debug.sideBySide ? "side-by-side-mode" : ""}`} style={styleVars}>
      {debug.sideBySide && (
        <div
          className="side-reference-viewport"
          style={{
            width: `${STAGE.width * scale}px`,
            height: `${STAGE.height * scale}px`,
          }}
          aria-label="Referencia lado a lado"
        >
          <div className="side-panel-label">Referencia</div>
          <img src="/assets/reference-cadenas.png" alt="Referencia visual" draggable="false" />
        </div>
      )}
      <div
        className="stage-viewport"
        style={{
          width: `${STAGE.width * scale}px`,
          height: `${STAGE.height * scale}px`,
        }}
      >
        <Stage
          debug={debug}
          onMousePosition={setMouse}
          onPin={setPin}
          pin={pin}
          pathPoints={pathPoints}
          onPathPointsChange={setPathPoints}
          selectedElementId={selectedElementId}
          calibrationOverrides={calibrationOverrides}
          hiddenCalibrationIds={hiddenCalibrationIds}
        />
      </div>
      <DebugPanel
        debug={debug}
        onChange={setDebug}
        mouse={mouse}
        pin={pin}
        scale={scale}
        pathD={debug.useGeneratedPathV2 ? GENERATED_SNAKE_V2_PATH_D : debug.useGeneratedPath ? GENERATED_SNAKE_PATH_D : buildSnakePaths(pathPoints).fullPath}
        pathConfigJson={pathConfigJson}
        layoutJson={JSON.stringify(LAYOUT, null, 2)}
        allCalibrationJson={allCalibrationJson}
        selectedElementId={selectedElementId}
        onSelectedElementIdChange={setSelectedElementId}
        calibrationOverrides={calibrationOverrides}
        onPatchCalibrationElement={patchCalibrationElement}
        onResetCalibrationElement={resetCalibrationElement}
        hiddenCalibrationIds={hiddenCalibrationIds}
        onHiddenCalibrationIdsChange={setHiddenCalibrationIds}
      />
    </div>
  );
}
