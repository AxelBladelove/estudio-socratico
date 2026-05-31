import type { DebugState } from "../App";
import type { CSSProperties, MouseEvent } from "react";
import Hotspot from "./Hotspot";
import LessonNode from "./LessonNode";
import LessonPath from "./LessonPath";
import {
  CALIBRATION_LAYOUT,
  LAYOUT,
  LESSONS,
  stagePointFromEvent,
  type CalibrationElement,
  type CalibrationElementId,
  type CalibrationOverrides,
} from "../lib/layout";
import type { PathPoint } from "../lib/snake";

interface Props {
  debug: DebugState;
  onMousePosition: (point: { x: number; y: number }) => void;
  onPin: (point: { x: number; y: number }) => void;
  pin: { x: number; y: number } | null;
  pathPoints: PathPoint[];
  onPathPointsChange: (points: PathPoint[]) => void;
  selectedElementId: CalibrationElementId;
  calibrationOverrides: CalibrationOverrides;
  hiddenCalibrationIds: CalibrationElementId[];
}

export interface CalibrationRenderApi {
  attrs: (id: CalibrationElementId) => Record<string, string | boolean>;
  style: (id: CalibrationElementId) => CSSProperties;
  element: (id: CalibrationElementId) => CalibrationElement;
}

function BackIcon({ style, attrs }: { style?: CSSProperties; attrs?: Record<string, string | boolean> }) {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" style={style} {...attrs}>
      <path d="M15.5 4.5 8 12l7.5 7.5" />
      <path d="M8.5 12H20" />
    </svg>
  );
}

function ListIcon({ style, attrs }: { style?: CSSProperties; attrs?: Record<string, string | boolean> }) {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" style={style} {...attrs}>
      <circle cx="5" cy="6" r="1.5" />
      <circle cx="5" cy="12" r="1.5" />
      <circle cx="5" cy="18" r="1.5" />
      <path d="M10 6h9M10 12h9M10 18h9" />
    </svg>
  );
}

function ArrowUpIcon({ style, attrs }: { style?: CSSProperties; attrs?: Record<string, string | boolean> }) {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" style={style} {...attrs}>
      <path d="M12 19V5" />
      <path d="m6.5 10.5 5.5-5.5 5.5 5.5" />
    </svg>
  );
}

export default function Stage({
  debug,
  onMousePosition,
  onPin,
  pin,
  pathPoints,
  onPathPointsChange,
  selectedElementId,
  calibrationOverrides,
  hiddenCalibrationIds,
}: Props) {
  const onMove = (event: MouseEvent<HTMLElement>) => {
    onMousePosition(stagePointFromEvent(event));
  };

  const onClick = (event: MouseEvent<HTMLElement>) => {
    if (!event.altKey) {
      return;
    }
    const point = stagePointFromEvent(event);
    onPin(point);
    console.log(`[stage] ALT click: x=${point.x}, y=${point.y}`);
  };

  const referenceStyle = {
    opacity: debug.showReference ? debug.referenceOpacity : 0,
    mixBlendMode: debug.referenceMode,
  } as CSSProperties;
  const element = (id: CalibrationElementId) => ({
    ...CALIBRATION_LAYOUT.elements[id],
    ...calibrationOverrides[id],
  });

  const visibleForMode = (id: CalibrationElementId) => {
    if (hiddenCalibrationIds.includes(id)) {
      return false;
    }
    const current = element(id);
    if (debug.calibrationView === "selected") {
      return id === selectedElementId;
    }
    if (debug.calibrationView === "text") {
      return current.type === "text";
    }
    if (debug.calibrationView === "icons") {
      return current.type === "svg";
    }
    if (debug.calibrationView === "progress") {
      return id.startsWith("progress") || id === "progressCard.box";
    }
    if (debug.calibrationView === "nodes") {
      return id.startsWith("node.") || id.startsWith("label.") || id.startsWith("cta.");
    }
    return true;
  };

  const style = (id: CalibrationElementId): CSSProperties => {
    const current = element(id);
    const transform = `scale(${current.scaleX ?? 1}, ${current.scaleY ?? 1}) rotate(${current.rotate ?? 0}deg)`;
    return {
      left: current.x,
      top: current.y,
      width: current.w,
      height: current.h,
      fontSize: current.fontSize,
      lineHeight: current.lineHeight ? `${current.lineHeight}px` : undefined,
      fontWeight: current.fontWeight,
      letterSpacing: current.letterSpacing !== undefined ? `${current.letterSpacing}px` : undefined,
      fontFamily: current.fontFamily,
      borderRadius: current.radius,
      transform,
      transformOrigin: "top left",
      display: visibleForMode(id) ? undefined : "none",
    };
  };

  const attrs = (id: CalibrationElementId) => ({
    "data-cal-id": id,
    "data-cal-type": element(id).type,
    "data-cal-selected": debug.calibrationMode && id === selectedElementId ? "true" : "false",
  });

  const calibrationApi: CalibrationRenderApi = { attrs, style, element };

  return (
    <main className="stage" onMouseMove={onMove} onClick={onClick} aria-label="Modulo 2 Cadenas de caracteres">
      <div className="background-layer" />
      <div className={`reference-layer ${debug.referencePlacement}`} style={referenceStyle}>
        <img src="/assets/reference-cadenas.png" alt="Referencia completa" draggable="false" />
      </div>
      <div className={`grid-layer ${debug.showGrid ? "visible" : ""}`} />
      <section
        className={`reconstructed-ui-layer ${debug.showUi ? "visible" : ""} ${debug.hideTextCheckDistractors ? "text-check" : ""} ${
          debug.calibrationMode ? "element-calibration-mode" : ""
        } ${debug.pathCalibrationMode ? "path-calibration-mode" : ""}`}
      >
        <button
          className="pill back-pill"
          style={style("backButton.box")}
          {...attrs("backButton.box")}
          type="button"
        >
          <BackIcon
            style={style("backButton.icon")}
            attrs={attrs("backButton.icon")}
          />
          <span
            style={style("backButton.text")}
            {...attrs("backButton.text")}
          >
            Fundamentos C
          </span>
        </button>
        <p className="stage-text module-label" style={style("moduleLabel.text")} {...attrs("moduleLabel.text")}>
          MÓDULO 2
        </p>
        <h1 className="stage-text main-title" style={style("mainTitle.text")} {...attrs("mainTitle.text")}>
          Cadenas de caracteres
        </h1>
        <span className="stage-text subtitle-text" style={style("subtitle.text")} {...attrs("subtitle.text")}>
          char, strings y texto
        </span>
        <button
          className="pill modules-pill"
          style={style("modulesButton.box")}
          {...attrs("modulesButton.box")}
          type="button"
        >
          <ListIcon
            style={style("modulesButton.icon")}
            attrs={attrs("modulesButton.icon")}
          />
          <span
            style={style("modulesButton.text")}
            {...attrs("modulesButton.text")}
          >
            Módulos
          </span>
        </button>

        <div
          className="progress-card"
          style={style("progressCard.box")}
          {...attrs("progressCard.box")}
        >
          <span
            className="metric-number"
            style={style("progress.leftNumber")}
            {...attrs("progress.leftNumber")}
          >
            18
          </span>
          <span
            className="metric-rest"
            style={style("progress.leftRest")}
            {...attrs("progress.leftRest")}
          >
            /64 ejercicios
          </span>
          <span
            className="metric-number"
            style={style("progress.rightNumber")}
            {...attrs("progress.rightNumber")}
          >
            28%
          </span>
          <span
            className="metric-rest"
            style={style("progress.rightRest")}
            {...attrs("progress.rightRest")}
          >
            completado
          </span>
          <div className="progress-bars" aria-hidden="true">
            {Array.from({ length: 14 }, (_, index) => {
              const id = `progress.segment.${String(index + 1).padStart(2, "0")}` as CalibrationElementId;
              return <span key={id} className={index < 4 ? "done" : index === 4 ? "partial" : ""} style={style(id)} {...attrs(id)} />;
            })}
          </div>
        </div>

        {["#include <stdio.h>", "", "int main() {", '  printf("Hola mundo");', "  return 0;"].map((line, index) => {
          const id = `code.line.${String(index + 1).padStart(2, "0")}` as CalibrationElementId;
          return (
            <div key={id} className="code-art" style={style(id)} {...attrs(id)}>
              {line || "\u00a0"}
            </div>
          );
        })}

        <LessonPath
          pathPoints={pathPoints}
          showPoints={debug.showPathPoints}
          onPathPointsChange={onPathPointsChange}
          useGeneratedPath={debug.useGeneratedPath}
          useGeneratedPathV2={debug.useGeneratedPathV2}
          showRawV2Path={debug.showRawV2Path}
          showExtractedPoints={debug.showExtractedPoints}
          showSkeletonOverlay={debug.showSkeletonOverlay}
          showMaskOverlay={debug.showMaskOverlay}
          showPathHandles={debug.showPathHandles}
          pathCalibrationMode={debug.pathCalibrationMode}
        />
        {LESSONS.map((lesson) => (
          <LessonNode key={lesson.id} lesson={lesson} calibration={calibrationApi} />
        ))}

        <svg
          className="abc-art"
          style={{
            left: LAYOUT.rightDecoration.x,
            top: LAYOUT.rightDecoration.y,
            width: LAYOUT.rightDecoration.w,
            height: LAYOUT.rightDecoration.h,
          }}
          viewBox="0 0 430 430"
          aria-hidden="true"
        >
          <defs>
            <filter id="abcGlow" x="-40%" y="-40%" width="180%" height="180%">
              <feGaussianBlur stdDeviation="5" result="blur" />
              <feMerge>
                <feMergeNode in="blur" />
                <feMergeNode in="SourceGraphic" />
              </feMerge>
            </filter>
          </defs>
          <path className="bubble" d="M110 51c4-35 29-55 65-51l112 14c38 5 61 31 57 70l-12 95c-4 37-32 60-69 56l-86-11-65 49 12-61c-25-10-39-33-35-64l21-97Z" />
          <path className="quote" d="M150 99c-18 5-27 18-27 39v8h31v38h-50v-40c0-33 14-55 42-65l4 20Zm75 0c-18 5-27 18-27 39v8h31v38h-50v-40c0-33 14-55 42-65l4 20Z" />
          <text x="147" y="205">abc</text>
          <path className="dashed" d="M317 166c64 26 82 94 42 132-51 48-145 29-145-36 0-41 30-68 69-78" />
          <ellipse cx="291" cy="303" rx="43" ry="29" transform="rotate(-25 291 303)" />
          <ellipse cx="351" cy="276" rx="43" ry="29" transform="rotate(-37 351 276)" />
          <g className="sparkles">
            <path d="M385 58v8M381 62h8" />
            <path d="M407 92v8M403 96h8" />
            <path d="M393 295v14M386 302h14" />
            <path d="M420 162v10M415 167h10" />
          </g>
        </svg>

        <footer
          className="bottom-nav"
          style={style("bottomNav.box")}
          {...attrs("bottomNav.box")}
        >
          <ListIcon
            style={style("bottomNav.menuIcon")}
            attrs={attrs("bottomNav.menuIcon")}
          />
          <p
            className="bottom-next-text"
            style={style("bottomNav.nextText")}
            {...attrs("bottomNav.nextText")}
          >
            Siguiente:
          </p>
          <p
            className="bottom-lesson-text"
            style={style("bottomNav.lessonText")}
            {...attrs("bottomNav.lessonText")}
          >
            Funciones string
          </p>
          <button
            type="button"
            aria-label="Continuar"
            style={style("bottomNav.arrowButton")}
            {...attrs("bottomNav.arrowButton")}
          >
            <ArrowUpIcon
              style={style("bottomNav.arrowIcon")}
              attrs={attrs("bottomNav.arrowIcon")}
            />
          </button>
        </footer>
      </section>
      <section className={`hotspot-layer ${debug.pathCalibrationMode ? "path-calibration-mode" : ""}`} aria-label="Hotspots">
        <Hotspot
          x={LAYOUT.header.backButton.x}
          y={LAYOUT.header.backButton.y}
          width={LAYOUT.header.backButton.w}
          height={LAYOUT.header.backButton.h}
          label="Fundamentos C"
          visible={debug.showHotspots}
        />
        <Hotspot
          x={LAYOUT.header.modulesButton.x}
          y={LAYOUT.header.modulesButton.y}
          width={LAYOUT.header.modulesButton.w}
          height={LAYOUT.header.modulesButton.h}
          label="Módulos"
          visible={debug.showHotspots}
        />
        <Hotspot
          x={LAYOUT.progress.card.x}
          y={LAYOUT.progress.card.y}
          width={LAYOUT.progress.card.w}
          height={LAYOUT.progress.card.h}
          label="Progreso"
          visible={debug.showHotspots}
        />
        {LESSONS.map((lesson) => (
          <Hotspot
            key={lesson.id}
            x={lesson.x - lesson.size / 2}
            y={lesson.y - lesson.size / 2}
            width={lesson.size}
            height={lesson.size}
            label={lesson.title}
            visible={debug.showHotspots}
          />
        ))}
        <Hotspot
          x={LAYOUT.bottomNav.card.x}
          y={LAYOUT.bottomNav.card.y}
          width={LAYOUT.bottomNav.card.w}
          height={LAYOUT.bottomNav.card.h}
          label="Bottom nav"
          visible={debug.showHotspots}
        />
      </section>
      {pin && (
        <div className="coord-pin" style={{ left: pin.x, top: pin.y }}>
          {pin.x}, {pin.y}
        </div>
      )}
    </main>
  );
}
