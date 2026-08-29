#!/usr/bin/env node
/**
 * Publishes a built installer to InstallerHub — the step that actually puts a
 * new version on users' machines.
 *
 *   node scripts/push-installer-update.mjs --file "<installer.exe>" --version 2.8.2
 *   ...same again with --yes to actually write. Without --yes it is a dry run.
 *
 * Environment:
 *   ADLM_ADMIN_TOKEN     access token for an admin account (required to write)
 *   ADLM_API_BASE_URL    defaults to https://api.adlmstudio.net
 *
 * What it does, in order:
 *   1. hashes the local file (sha256)
 *   2. reads the CURRENT deployment record for the product
 *   3. presigns an R2 upload and PUTs the bytes straight to the bucket
 *   4. writes the record back with the new packageUri, version and sha256
 *   5. reads it again and checks all three landed
 *
 * Two things this exists to get right:
 *
 * The hash. InstallerHub verifies the downloaded package against `sha256` and
 * refuses to install on mismatch. Upload a new package while the old hash is
 * still on the record and you have not shipped an update — you have broken
 * every install until someone notices.
 *
 * The read-modify-write. PUT /admin/deployments/:productKey replaces the
 * record: `normalizeDeployment` rebuilds operations, installArguments,
 * waitForExit, requiresElevation, displayName, enabled and notes from the
 * request body, defaulting anything absent. A PUT carrying only the new URI
 * and version would silently wipe the install operations. So this reads the
 * record first and sends it back whole, with only the three fields changed.
 *
 * envVars and localRandomVars are the exception: the route preserves them when
 * the key is ABSENT from the body, so this deletes them from the payload
 * rather than echoing them. They hold live secrets and should not make a round
 * trip through this script.
 */

import { createHash } from "node:crypto";
import { readFile } from "node:fs/promises";
import { basename } from "node:path";

const DEFAULT_API = "https://api.adlmstudio.net";

function parseArgs(argv) {
  const args = { product: "rategen", yes: false };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === "--yes") args.yes = true;
    else if (a === "--allow-kind-change") args.allowKindChange = true;
    else if (a === "--file") args.file = argv[++i];
    else if (a === "--version") args.version = argv[++i];
    else if (a === "--product") args.product = argv[++i];
    else if (a === "--api") args.api = argv[++i];
    else {
      console.error(`Unknown argument: ${a}`);
      process.exit(2);
    }
  }
  return args;
}

function die(message) {
  console.error(`\n✗ ${message}`);
  process.exit(1);
}

async function api(base, token, path, { method = "GET", body } = {}) {
  const res = await fetch(`${base}${path}`, {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
      ...(body ? { "Content-Type": "application/json" } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
  });

  const text = await res.text();
  let json;
  try {
    json = text ? JSON.parse(text) : {};
  } catch {
    json = { raw: text };
  }

  if (!res.ok) {
    const reason = json?.error || json?.raw || res.statusText;
    // 401 here is nearly always an expired token: access tokens last 15
    // minutes, and a release takes longer than that to get to this point.
    const hint =
      res.status === 401
        ? " (access tokens expire after 15 minutes — get a fresh one and re-run)"
        : "";
    die(`${method} ${path} → ${res.status}: ${reason}${hint}`);
  }

  return json;
}

const args = parseArgs(process.argv.slice(2));
const base = (args.api || process.env.ADLM_API_BASE_URL || DEFAULT_API).replace(/\/+$/, "");
const token = String(process.env.ADLM_ADMIN_TOKEN || "").trim();

if (!args.file) die("--file is required (path to the built installer)");
if (!args.version) die("--version is required (e.g. 2.8.2)");
if (!token) die("ADLM_ADMIN_TOKEN is not set — it needs an admin account's access token");

const bytes = await readFile(args.file).catch(() =>
  die(`Cannot read ${args.file}. Build the installer first.`),
);
const sha256 = createHash("sha256").update(bytes).digest("hex");
const fileName = basename(args.file);
const sizeMb = (bytes.length / (1024 * 1024)).toFixed(1);

console.log(`\nPackage   ${fileName}  (${sizeMb} MB)`);
console.log(`sha256    ${sha256}`);
console.log(`Product   ${args.product}`);
console.log(`API       ${base}`);

// ── 1. current record ────────────────────────────────────────────────────────
const { item: current } = await api(base, token, `/admin/deployments/${args.product}`);
if (!current) die(`No deployment record for "${args.product}". Refusing to invent one.`);

// Kind is inferred from the file extension, exactly as the server does it, so
// what is printed here is what will be stored.
const kind = fileName.toLowerCase().endsWith(".zip") ? "zip" : "file";
const liveKind = String(current.packageKind || "").trim() || "(unset)";

console.log(`\nLive now  version ${current.version || "(unset)"}  [${liveKind}]`);
console.log(`          ${current.packageUri || "(no package)"}`);
console.log(`Shipping  version ${args.version}  [${kind}]`);

if (current.version === args.version) {
  console.log(
    `\n! The record already says ${args.version}. Continuing will replace that package.`,
  );
}

// The kind and the operations have to agree, and nothing else checks that.
//
// On 28 Aug 2026 this script pushed an Inno installer .exe over a deployment
// whose operations were `copyDirectory` from `source: "app"` — zip semantics.
// It preserved the operations perfectly and changed the package out from under
// them, so the record verified clean and could not install: there is no app/
// directory inside a setup executable. The version was unchanged too, so the
// breakage stayed mostly invisible.
//
// Changing kind is therefore a decision about how the product installs, not a
// detail of which file you happened to point at. It needs saying out loud.
if (liveKind !== "(unset)" && kind !== liveKind) {
  const ops = (current.operations || []).map((o) => `${o.type}(${o.source || "."})`);
  console.log(`\n! PACKAGE KIND CHANGES: ${liveKind} → ${kind}`);
  console.log(`  The stored operations are: ${ops.join(", ") || "(none)"}`);
  console.log(`  A "zip" package is extracted and its folders copied by those`);
  console.log(`  operations; a "file" package is not. If they no longer match,`);
  console.log(`  the record will verify clean and still fail to install.`);
  if (!args.allowKindChange) {
    die("Refusing. Ship the matching package type, or pass --allow-kind-change if this is deliberate.");
  }
  console.log("  --allow-kind-change given; continuing.");
}

if (!args.yes) {
  console.log("\nDry run — nothing was uploaded or changed. Re-run with --yes to ship.");
  process.exit(0);
}

// ── 2. upload the bytes ──────────────────────────────────────────────────────
// /presign-package, not /upload-package: the latter proxies bytes through
// Lambda and cannot carry anything over ~4.4 MB.
console.log("\nUploading…");
const presigned = await api(base, token, `/admin/deployments/presign-package`, {
  method: "POST",
  body: { fileName, contentType: "application/octet-stream" },
});

// createPresignedPutUrl signs only ContentType, so it is the one header that
// has to be replayed exactly as handed back — anything else breaks the
// signature and R2 answers 403.
//
// Retried, because this leg is a single ~85 MB PUT over whatever connection the
// build machine has, and it died once with ECONNRESET mid-transfer. A dropped
// upload is safe — the deployment record is only written afterwards, so the
// live version is untouched — but it costs a full re-run of build, zip and
// login, and the token has 15 minutes on it. The presigned URL is good for 15
// minutes too, so the same one can be reused for a retry.
async function uploadWithRetry(attempts = 3) {
  for (let attempt = 1; attempt <= attempts; attempt++) {
    try {
      const put = await fetch(presigned.uploadUrl, {
        method: "PUT",
        headers: presigned.headers,
        body: bytes,
      });

      // A refusal is not a flake: a 403 means the signature or Content-Type is
      // wrong and every retry will fail identically. Only the transport is
      // worth retrying.
      if (!put.ok) die(`R2 upload failed → ${put.status} ${put.statusText}`);
      return;
    } catch (err) {
      const reason = err?.cause?.code || err?.code || err?.message || String(err);
      if (attempt === attempts) {
        die(`R2 upload failed after ${attempts} attempts (${reason}). Nothing was published; the live version is unchanged.`);
      }
      const waitMs = attempt * 3000;
      console.log(`  upload attempt ${attempt} failed (${reason}) — retrying in ${waitMs / 1000}s`);
      await new Promise((r) => setTimeout(r, waitMs));
    }
  }
}

await uploadWithRetry();
console.log(`Uploaded  ${presigned.packageUri}`);

// ── 3. write the record back, whole ──────────────────────────────────────────
const payload = { ...current };
for (const key of ["_id", "__v", "createdAt", "updatedAt", "createdBy", "updatedBy"]) {
  delete payload[key];
}
// Absent means "leave what is stored" for these two. See the header comment.
delete payload.envVars;
delete payload.localRandomVars;

payload.packageUri = presigned.packageUri;
payload.packageKind = presigned.packageKind || "file";
payload.version = args.version;
payload.sha256 = sha256;

const { item: saved } = await api(base, token, `/admin/deployments/${args.product}`, {
  method: "PUT",
  body: payload,
});

// ── 4. verify ────────────────────────────────────────────────────────────────
const { item: check } = await api(base, token, `/admin/deployments/${args.product}`);
const problems = [];
if (check.version !== args.version) problems.push(`version is ${check.version}`);
if (check.sha256 !== sha256) problems.push(`sha256 is ${check.sha256 || "(unset)"}`);
if (check.packageUri !== presigned.packageUri) problems.push(`packageUri is ${check.packageUri}`);
if (check.packageKind !== kind) problems.push(`packageKind is ${check.packageKind}`);
if (check.enabled === false) problems.push("the deployment is disabled");
if ((saved.operations?.length ?? 0) !== (current.operations?.length ?? 0)) {
  problems.push(
    `operations count changed ${current.operations?.length ?? 0} → ${saved.operations?.length ?? 0}`,
  );
}

if (problems.length) {
  die(`Record did not land as expected: ${problems.join("; ")}`);
}

console.log(`\n✓ ${args.product} ${args.version} is live for users.`);
console.log("  Confirm on a real machine: InstallerHub should offer the update,");
console.log("  and after installing, Custom Rate should show the Build with AI panel.");
