import { readFileSync, existsSync, unlinkSync } from "node:fs";
import { join } from "node:path";

const STATE_DIR = join(".claude", "sprint-sessions");

function main() {
  const input = JSON.parse(readFileSync(0, "utf8"));
  const stateFile = join(STATE_DIR, `${input.session_id || "unknown"}.json`);
  if (!existsSync(stateFile)) return 0;

  const state = JSON.parse(readFileSync(stateFile, "utf8"));
  if (state.codeEdited && !state.sprintLogged) {
    if (input.stop_hook_active) return 0;
    process.stderr.write(
      `SPRINT LOG MISSING: update the relevant file in tasks/sprints before stopping.\nModified: ${state.files.join(", ")}`
    );
    return 2;
  }

  try { unlinkSync(stateFile); } catch { /* Cleanup is best effort. */ }
  return 0;
}

let code = 0;
try { code = main(); } catch { code = 0; }
process.exit(code);

