import assert from "node:assert/strict";
import { createServer } from "node:http";
import { mkdir, readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "@playwright/test";

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(scriptDirectory, "..", "..");
const webRoot = path.join(repositoryRoot, "src", "SimpleServiceChecker.App", "Web");
const outputDirectory = path.join(repositoryRoot, "output", "playwright");
const contentTypes = new Map([
  [".css", "text/css; charset=utf-8"],
  [".html", "text/html; charset=utf-8"],
  [".js", "text/javascript; charset=utf-8"],
]);

function startStaticServer() {
  const server = createServer(async (request, response) => {
    try {
      const pathname = decodeURIComponent(new URL(request.url ?? "/", "http://127.0.0.1").pathname);
      if (pathname === "/favicon.ico") {
        response.writeHead(204).end();
        return;
      }
      const relativePath = pathname === "/" ? "index.html" : pathname.replace(/^\/+/, "");
      const requestedPath = path.resolve(webRoot, relativePath);
      const webPrefix = `${path.resolve(webRoot)}${path.sep}`.toLowerCase();
      if (!requestedPath.toLowerCase().startsWith(webPrefix)) {
        response.writeHead(403).end("Forbidden");
        return;
      }
      const content = await readFile(requestedPath);
      response.writeHead(200, {
        "Cache-Control": "no-store",
        "Content-Type": contentTypes.get(path.extname(requestedPath)) ?? "application/octet-stream",
      });
      response.end(content);
    } catch (error) {
      response.writeHead(error?.code === "ENOENT" ? 404 : 500).end("Unavailable");
    }
  });
  return new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(0, "127.0.0.1", () => resolve(server));
  });
}

const coverage = {
  fileName: "Synthetic Coverage.xlsx",
  activatedAtUtc: "2026-09-15T12:00:00Z",
  towerCount: 3,
  fibreRangeCount: 4,
  warningCount: 1,
  warnings: ["Synthetic row warning: a disabled tower is shown in gray."],
  schemaVersion: "1",
  applicationSchemaVersion: "1",
  canRestorePrevious: true,
  towers: [],
};

const locationResult = {
  revision: 0,
  latitude: 46.521,
  longitude: -84.346,
  formattedAddress: "100 Example Road, Demo City",
  fibre: { status: "available", maxSpeeds: "150/150" },
  candidates: [
    { rowNumber: 2, name: "North Demo Tower", latitude: 46.55, longitude: -84.32, distanceKilometres: 4.2, maxDownloadMbps: 110, maxUploadMbps: 33, heightFeet: 180, rangeKilometres: 12, terrainSampleCount: 33, notes: "Synthetic visual-test candidate." },
    { rowNumber: 3, name: "River Demo Tower", latitude: 46.49, longitude: -84.4, distanceKilometres: 6.8, maxDownloadMbps: 55, maxUploadMbps: 11, heightFeet: 150, rangeKilometres: 10, terrainSampleCount: 33 },
    { rowNumber: 4, name: "East Demo Tower", latitude: 46.53, longitude: -84.25, distanceKilometres: 8.5, maxDownloadMbps: 50, maxUploadMbps: 10, heightFeet: 120, rangeKilometres: 9, terrainSampleCount: 33 },
  ],
};

function terrainProfile() {
  const sampleCount = 33;
  const distanceMetres = 4200;
  const samples = Array.from({ length: sampleCount }, (_, index) => {
    const ratio = index / (sampleCount - 1);
    const lineOfSightMetres = 248 + (262 - 248) * ratio;
    const firstFresnelRadiusMetres = 10.8 * Math.sin(Math.PI * ratio);
    const terrainMetres = 223 + 14 * Math.sin(ratio * Math.PI * 2.4) + 5 * Math.sin(ratio * Math.PI * 7);
    const clutterTopMetres = terrainMetres + 15.24;
    const clearance = lineOfSightMetres - clutterTopMetres;
    const clearanceCategory = clearance <= 0 ? "black" : clearance <= 3.048 ? "red" : clearance <= 9.144 ? "orange" : clearance <= 15.24 ? "yellow" : "green";
    return {
      distanceMetres: distanceMetres * ratio,
      latitude: 46.521 + .029 * ratio,
      longitude: -84.346 + .026 * ratio,
      terrainMetres,
      clutterTopMetres,
      lineOfSightMetres,
      firstFresnelRadiusMetres,
      fresnelReferenceRadiusMetres: firstFresnelRadiusMetres * .6,
      clearanceCategory,
    };
  });
  return {
    locationRevision: 0,
    plotRevision: 1,
    distanceMetres,
    bearingDegrees: 32.4,
    minimumClearanceMetres: -2.1,
    minimumClearanceDistanceMetres: 1968.75,
    minimumFresnelMarginMetres: -7.3,
    minimumFresnelMarginDistanceMetres: 2100,
    frequencyGigahertz: 5.8,
    fresnelClearancePercent: 60,
    earthModel: "standard",
    clutterHeightMetres: 15.24,
    customerHeightMetres: 7.62,
    towerHeightMetres: 54.864,
    sampleSpacingMetres: distanceMetres / (sampleCount - 1),
    maximumResolutionMetres: 38.2,
    isLineOfSightClear: false,
    isFresnelReferenceClear: false,
    samples,
  };
}

async function renderScenario(page, width, height) {
  await page.setViewportSize({ width, height });
  await page.addInitScript(() => {
    const listeners = [];
    globalThis.chrome = {
      webview: {
        addEventListener(type, listener) {
          if (type === "message") listeners.push(listener);
        },
        postMessage(message) {
          (globalThis.__syntheticBridgeMessages ??= []).push(message);
        },
      },
    };
  });
  await page.goto(globalThis.__visualSmokeUrl, { waitUntil: "networkidle" });
  await page.addStyleTag({ content: `
    #map { background:
      linear-gradient(34deg, rgba(29, 75, 66, .42), rgba(41, 85, 59, .18)),
      repeating-linear-gradient(118deg, #77906e 0 34px, #8da17d 34px 72px, #667f68 72px 104px);
    }
    #map .map-placeholder { color: #fff; background: radial-gradient(circle at 58% 42%, rgba(55, 112, 151, .8) 0 11%, transparent 12%), linear-gradient(135deg, rgba(38, 72, 50, .12), rgba(27, 58, 45, .52)); text-shadow: 0 1px 3px #233; }
    #map .map-placeholder strong { color: #fff; }
  ` });
  await page.evaluate(({ coveragePayload, locationPayload, profilePayload }) => {
    globalThis.simpleServiceChecker.receive({ type: "setupState", payload: { apiKeyConfigured: true, workbookConfigured: true, canRestorePrevious: true, version: "1.0.0" } });
    globalThis.simpleServiceChecker.receive({ type: "coverageLoaded", payload: coveragePayload });
    globalThis.simpleServiceChecker.receive({ type: "locationResult", payload: locationPayload });
    const placeholder = document.querySelector("#map .map-placeholder");
    placeholder?.replaceChildren(Object.assign(document.createElement("strong"), { textContent: "Synthetic map canvas" }), Object.assign(document.createElement("span"), { textContent: "No Google request or company data is used by this test." }));
    document.querySelector(".plot-button")?.click();
    globalThis.simpleServiceChecker.receive({ type: "terrainProfileResult", payload: profilePayload });
  }, { coveragePayload: coverage, locationPayload: locationResult, profilePayload: terrainProfile() });

  await page.waitForFunction(() => {
    const profile = document.querySelector("#profile-card");
    const output = document.querySelector("#profile-output");
    const canvas = document.querySelector("#terrain-chart");
    return profile && !profile.hidden && output && !output.hidden && canvas && canvas.width > 0 && canvas.height > 0;
  });

  const layout = await page.evaluate(() => {
    const rectangle = selector => {
      const bounds = document.querySelector(selector)?.getBoundingClientRect();
      return bounds ? { x: bounds.x, y: bounds.y, width: bounds.width, height: bounds.height, right: bounds.right, bottom: bounds.bottom } : null;
    };
    const profile = document.querySelector("#profile-card");
    const controls = document.querySelector(".profile-controls");
    return {
      viewport: { width: innerWidth, height: innerHeight },
      body: { width: document.body.scrollWidth, height: document.body.scrollHeight },
      sidebar: rectangle("#sidebar"),
      workspace: rectangle(".workspace"),
      legend: rectangle(".top-right"),
      profile: rectangle("#profile-card"),
      chart: rectangle("#terrain-chart"),
      profileOverflow: profile.scrollWidth - profile.clientWidth,
      controlsOverflow: controls.scrollWidth - controls.clientWidth,
      title: document.querySelector("h1")?.textContent,
      candidateCount: document.querySelector("#candidate-count")?.textContent,
      fibreStatus: document.querySelector("#fibre-result strong")?.textContent,
    };
  });

  assert.equal(layout.title, "Starling Service Checker");
  assert.equal(layout.candidateCount, "3");
  assert.equal(layout.fibreStatus, "Available in workbook");
  assert.ok(layout.sidebar && layout.workspace && layout.legend && layout.profile && layout.chart, "Required visual regions must render.");
  assert.ok(layout.sidebar.width >= 320 && layout.sidebar.right <= width, "Sidebar must fit within the viewport.");
  assert.ok(layout.workspace.width >= 700 && layout.workspace.right <= width + 1, "Map workspace must remain usable.");
  assert.ok(layout.legend.x >= layout.workspace.x && layout.legend.right <= width, "Map legends must remain inside the workspace.");
  assert.ok(layout.profile.x >= layout.workspace.x && layout.profile.right <= width && layout.profile.bottom <= height, "Terrain profile must remain inside the workspace.");
  assert.ok(layout.profile.height >= 390, "Terrain profile must retain its approved height.");
  assert.ok(layout.chart.width >= 600 && layout.chart.height >= 150, "Terrain chart must retain a usable plotting area.");
  assert.ok(layout.body.width <= width && layout.body.height <= height, "The application shell must not create document-level overflow.");
  assert.ok(layout.profileOverflow <= 1 && layout.controlsOverflow <= 1, "Profile content must not overflow horizontally.");

  const screenshotPath = path.join(outputDirectory, `starling-${width}x${height}.png`);
  const screenshot = await page.screenshot({ path: screenshotPath, fullPage: false });
  assert.ok(screenshot.length > 40_000, "Visual smoke screenshot is unexpectedly small or blank.");

  await page.locator("#profile-minimize").click();
  const minimizedProfile = await page.evaluate(() => {
    const profile = document.querySelector("#profile-card");
    const bounds = profile.getBoundingClientRect();
    return {
      minimized: profile.classList.contains("minimized"),
      height: bounds.height,
      bottom: bounds.bottom,
      label: document.querySelector("#profile-minimize").getAttribute("aria-label"),
    };
  });
  assert.equal(minimizedProfile.minimized, true);
  assert.equal(minimizedProfile.label, "Restore terrain profile");
  assert.ok(minimizedProfile.height <= 90 && minimizedProfile.bottom <= height, "Minimized profile must fit at the bottom of the map.");

  await page.locator("#profile-minimize").click();
  assert.equal(await page.locator("#profile-card").evaluate(element => element.classList.contains("minimized")), false);
  assert.equal(await page.locator("#profile-minimize").getAttribute("aria-label"), "Minimize terrain profile");
  await page.locator("#profile-close").click();
  assert.equal(await page.locator("#profile-card").evaluate(element => element.hidden), true);

  const fibreStates = [
    ["available", "Available in workbook"],
    ["notlisted", "Not listed"],
    ["addressrequired", "Address required"],
    ["dataerror", "Data error"],
  ];
  for (const [status, expectedHeading] of fibreStates) {
    await page.evaluate(({ locationPayload, fibreStatus }) => {
      globalThis.simpleServiceChecker.receive({
        type: "locationResult",
        payload: { ...locationPayload, fibre: { status: fibreStatus }, candidates: [] },
      });
    }, { locationPayload: locationResult, fibreStatus: status });
    assert.equal(await page.locator("#fibre-result strong").textContent(), expectedHeading);
    assert.equal(await page.locator("#candidate-count").textContent(), "0");
    await expectText(page, "#candidate-list", "No towers include this location within their published range.");
  }

  await page.evaluate(locationPayload => {
    globalThis.simpleServiceChecker.receive({
      type: "locationResult",
      payload: { ...locationPayload, candidates: [locationPayload.candidates[1]] },
    });
  }, locationResult);
  assert.equal(await page.locator("#candidate-count").textContent(), "1");
  await expectText(page, "#candidate-list", "1. River Demo Tower");

  await page.evaluate(locationPayload => {
    globalThis.simpleServiceChecker.receive({
      type: "locationResult",
      payload: { ...locationPayload, revision: 999, candidates: locationPayload.candidates },
    });
  }, locationResult);
  assert.equal(await page.locator("#candidate-count").textContent(), "1", "A stale location result must not replace the active result.");

  await page.evaluate(() => document.activeElement?.blur());
  await page.keyboard.press("Tab");
  await page.locator("#settings-toggle").focus();
  assert.equal(await page.evaluate(() => document.activeElement?.id), "settings-toggle");
  const focusStyle = await page.locator("#settings-toggle").evaluate(element => {
    const style = getComputedStyle(element);
    return { outlineStyle: style.outlineStyle, outlineWidth: style.outlineWidth };
  });
  assert.notEqual(focusStyle.outlineStyle, "none");
  assert.notEqual(focusStyle.outlineWidth, "0px");

  return { screenshotPath, bytes: screenshot.length, layout };
}

async function expectText(page, selector, expected) {
  const textContent = await page.locator(selector).textContent();
  assert.ok(textContent?.includes(expected), `Expected ${selector} to contain "${expected}", received "${textContent}".`);
}

await mkdir(outputDirectory, { recursive: true });
const server = await startStaticServer();
const address = server.address();
assert.ok(address && typeof address === "object");
globalThis.__visualSmokeUrl = `http://127.0.0.1:${address.port}/`;
const browser = await chromium.launch({ channel: process.env.PLAYWRIGHT_CHANNEL ?? "msedge", headless: true });

try {
  const results = [];
  for (const viewport of [{ width: 1366, height: 768 }, { width: 1920, height: 1080 }]) {
    const page = await browser.newPage();
    const consoleErrors = [];
    page.on("console", message => {
      if (message.type() === "error") consoleErrors.push(message.text());
    });
    page.on("pageerror", error => consoleErrors.push(error.message));
    results.push(await renderScenario(page, viewport.width, viewport.height));
    assert.deepEqual(consoleErrors, [], `Browser console errors at ${viewport.width}x${viewport.height}: ${consoleErrors.join(" | ")}`);
    await page.close();
  }
  for (const result of results) {
    console.log(`Visual smoke passed: ${path.relative(repositoryRoot, result.screenshotPath)} (${result.bytes} bytes, chart ${Math.round(result.layout.chart.width)}x${Math.round(result.layout.chart.height)}).`);
  }
  console.log("Interaction smoke passed: fibre result states, zero/one/many candidate rendering, stale-result rejection, profile minimize/restore/close, and visible keyboard focus.");
} finally {
  await browser.close();
  await new Promise(resolve => server.close(resolve));
}
