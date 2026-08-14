import { readFileSync, mkdirSync, writeFileSync, existsSync } from "node:fs";
import { join } from "node:path";

const STATE_DIR = join(".claude", "sprint-sessions");
const CODE_ROOTS = ["ddac/"];

function classify(filePath) {
  const path = filePath.replace(/\\/g, "/").toLowerCase();
  if (path.includes("/bin/") || path.includes("/obj/")) return null;
  if (path.includes("tasks/sprints/")) return "sprint";
  if (CODE_ROOTS.some((root) => path.includes(root))) return "code";
  return null;
}

function main() {
  const input = JSON.parse(readFileSync(0, "utf8"));
  const sessionId = input.session_id || "unknown";
  const filePath = input.tool_input?.file_path || "";
  const kind = classify(filePath);
  if (!kind) return;

  mkdirSync(STATE_DIR, { recursive: true });
  const stateFile = join(STATE_DIR, `${sessionId}.json`);
  const state = existsSync(stateFile)
    ? JSON.parse(readFileSync(stateFile, "utf8"))
    : { codeEdited: false, sprintLogged: false, files: [] };

  const next = {
    codeEdited: state.codeEdited || kind === "code",
    sprintLogged: state.sprintLogged || kind === "sprint",
    files: state.files.includes(filePath) ? state.files : [...state.files, filePath],
  };
  writeFileSync(stateFile, JSON.stringify(next, null, 2));
}

try { main(); } catch { /* Bookkeeping must never break development. */ }

