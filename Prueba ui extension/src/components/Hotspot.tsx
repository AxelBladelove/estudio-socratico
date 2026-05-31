interface Props {
  x: number;
  y: number;
  width: number;
  height: number;
  label: string;
  visible: boolean;
}

export default function Hotspot({ x, y, width, height, label, visible }: Props) {
  return (
    <button
      className={`hotspot ${visible ? "visible" : ""}`}
      style={{ left: x, top: y, width, height }}
      type="button"
      aria-label={label}
      data-tip={label}
      onClick={() => console.log(`[hotspot] ${label}`)}
    />
  );
}
