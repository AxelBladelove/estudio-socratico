import type { CalibrationRenderApi } from "./Stage";
import type { CalibrationElementId, Lesson } from "../lib/layout";

function CheckIcon() {
  return (
    <svg viewBox="0 0 64 64" aria-hidden="true">
      <path d="M17 33.5 27.5 44 48 20" />
    </svg>
  );
}

function LockIcon() {
  return (
    <svg viewBox="0 0 64 64" aria-hidden="true">
      <path d="M20 28v-7c0-8 5-14 12-14s12 6 12 14v7" />
      <rect x="16" y="27" width="32" height="27" rx="7" />
      <path d="M32 38v8" />
    </svg>
  );
}

function StarIcon() {
  return (
    <svg viewBox="0 0 64 64" aria-hidden="true">
      <path d="m32 7 7.5 15.4 17 2.4-12.3 12 2.9 16.9L32 45.7 16.9 53.7l2.9-16.9-12.3-12 17-2.4L32 7Z" />
    </svg>
  );
}

function QuoteIcon() {
  return (
    <svg viewBox="0 0 64 64" aria-hidden="true" className="quote-icon">
      <rect x="11" y="11" width="42" height="42" rx="6" />
      <path d="M22 29c0-6 3-10 9-12l1 4c-3 1-5 4-5 8h6v12H22V29Z" />
      <text x="18" y="50">abc</text>
    </svg>
  );
}

export default function LessonNode({ lesson, calibration }: { lesson: Lesson; calibration: CalibrationRenderApi }) {
  const nodeId = `node.${lesson.id}.box` as CalibrationElementId;
  const iconId = `node.${lesson.id}.icon` as CalibrationElementId;
  const titleId = `label.${lesson.id}.title` as CalibrationElementId;
  const subtitleId = `label.${lesson.id}.subtitle` as CalibrationElementId;
  const sideDotId = "node.strings.sideDot" as CalibrationElementId;

  return (
    <>
      <button
        className={`lesson-node ${lesson.status}`}
        style={calibration.style(nodeId)}
        {...calibration.attrs(nodeId)}
        type="button"
        aria-label={`${lesson.title}: ${lesson.subtitle}`}
        onClick={() => console.log(`[lesson] ${lesson.id}`)}
      >
        <span className="node-material-layer node-contact-glow" aria-hidden="true" />
        <span className="node-material-layer node-outer-ring" aria-hidden="true" />
        <span className="node-material-layer node-ray-raise" aria-hidden="true" />
        <span className="node-material-layer node-inner-shine" aria-hidden="true" />
        {lesson.status === "completed" && (
          <span className="node-icon" style={calibration.style(iconId)} {...calibration.attrs(iconId)}>
            <CheckIcon />
          </span>
        )}
        {lesson.status === "current" && (
          <>
            <span className="node-icon" style={calibration.style(iconId)} {...calibration.attrs(iconId)}>
              <QuoteIcon />
            </span>
            <span className="current-side-dot" style={calibration.style(sideDotId)} {...calibration.attrs(sideDotId)} />
          </>
        )}
        {lesson.status === "locked" && (
          <span className="node-icon" style={calibration.style(iconId)} {...calibration.attrs(iconId)}>
            <LockIcon />
          </span>
        )}
        {lesson.status === "challenge" && (
          <span className="node-icon" style={calibration.style(iconId)} {...calibration.attrs(iconId)}>
            <StarIcon />
          </span>
        )}
      </button>
      <div className={`lesson-label ${lesson.status}`}>
        <h3
          className="lesson-text"
          style={calibration.style(titleId)}
          {...calibration.attrs(titleId)}
        >
          {lesson.title}
        </h3>
        <p
          className="lesson-text"
          style={calibration.style(subtitleId)}
          {...calibration.attrs(subtitleId)}
        >
          {lesson.subtitle}
        </p>
        {lesson.status === "current" && (
          <button
            className="lesson-text"
            type="button"
            style={calibration.style("cta.box")}
            {...calibration.attrs("cta.box")}
          >
            <span className="cta-text" style={calibration.style("cta.text")} {...calibration.attrs("cta.text")}>
              SIGUE AQUÍ
            </span>
          </button>
        )}
      </div>
    </>
  );
}
