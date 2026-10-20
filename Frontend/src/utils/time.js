const units = [
  ["y", 31536000],
  ["w", 604800],
  ["d", 86400],
  ["h", 3600],
  ["m", 60],
];

/**
 * The API stores UTC, but SQL Server hands it back without an offset
 * ("2026-10-10T06:45:18"), which browsers read as local time. Treat those as UTC.
 */
export function parseApiDate(value) {
  if (typeof value === "string" && /T\d{2}:\d{2}(:\d{2}(\.\d+)?)?$/.test(value)) {
    return new Date(`${value}Z`);
  }
  return new Date(value);
}

/** Instagram-style compact relative time: "3h", "2d", "now". */
export function timeAgo(value) {
  if (!value) return "";
  const date = parseApiDate(value);
  const seconds = Math.floor((Date.now() - date.getTime()) / 1000);
  if (Number.isNaN(seconds)) return "";
  if (seconds < 60) return "now";
  for (const [label, size] of units) {
    if (seconds >= size) return `${Math.floor(seconds / size)}${label}`;
  }
  return "now";
}

export function formatCount(count) {
  if (count >= 1_000_000) return `${(count / 1_000_000).toFixed(1).replace(/\.0$/, "")}M`;
  if (count >= 10_000) return `${(count / 1_000).toFixed(1).replace(/\.0$/, "")}K`;
  return count.toLocaleString();
}
