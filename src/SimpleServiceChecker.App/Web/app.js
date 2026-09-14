(() => {
  "use strict";

  const bridgeVersion = 1;
  const localOrigin = "https://app.simpleservicechecker.local";
  const state = {
    map: null,
    googleLoadPromise: null,
    chart: null,
  };

  const elements = {
    googleKey: document.querySelector("#google-key"),
    saveKey: document.querySelector("#save-key"),
    clearKey: document.querySelector("#clear-key"),
    keyBadge: document.querySelector("#key-badge"),
    chooseWorkbook: document.querySelector("#choose-workbook"),
    workbookBadge: document.querySelector("#workbook-badge"),
    workbookResult: document.querySelector("#workbook-result"),
    mapProof: document.querySelector("#map-proof"),
    placesProof: document.querySelector("#places-proof"),
    elevationProof: document.querySelector("#elevation-proof"),
    proofMessage: document.querySelector("#proof-message"),
    placeControl: document.querySelector("#place-control"),
    originLabel: document.querySelector("#origin-label"),
  };

  function requestId() {
    return globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random()}`;
  }

  function post(type, payload = {}) {
    if (!globalThis.chrome?.webview) {
      showMessage("The secure desktop bridge is not available.", "error");
      return;
    }

    globalThis.chrome.webview.postMessage({
      version: bridgeVersion,
      type,
      requestId: requestId(),
      payload,
    });
  }

  function setBadge(element, text, kind) {
    element.textContent = text;
    element.className = `badge ${kind}`;
  }

  function setProof(element, status) {
    element.className = `proof-dot ${status}`;
  }

  function resetProof() {
    setProof(elements.mapProof, "");
    setProof(elements.placesProof, "");
    setProof(elements.elevationProof, "");
  }

  function showMessage(text, kind = "muted") {
    elements.proofMessage.textContent = text;
    elements.proofMessage.className = `result ${kind}`;
  }

  function receive(message) {
    const envelope = typeof message === "string" ? JSON.parse(message) : message;
    const payload = envelope.payload ?? {};

    switch (envelope.type) {
      case "setupState":
        elements.originLabel.textContent = globalThis.location.origin === localOrigin
          ? "Secure local HTTPS application origin"
          : "Unexpected application origin";
        if (payload.apiKeyConfigured) {
          setBadge(elements.keyBadge, "Saved locally", "success");
          showMessage("Loading the saved company key…");
        }
        break;
      case "googleKeySaved":
        elements.googleKey.value = "";
        setBadge(elements.keyBadge, "Saved locally", "success");
        showMessage("Loading Google services…");
        break;
      case "googleKeyRejected":
        setBadge(elements.keyBadge, "Check key", "error");
        showMessage(payload.message ?? "The key could not be saved.", "error");
        break;
      case "googleKeyCleared":
        elements.googleKey.value = "";
        setBadge(elements.keyBadge, "Not configured", "neutral");
        resetProof();
        showMessage("The saved key was removed from this Windows profile.");
        break;
      case "workbookProbeResult":
        showWorkbookResult(payload);
        break;
      case "bridgeError":
      case "operationFailed":
        showMessage(payload.message ?? "An unexpected application error occurred.", "error");
        break;
      default:
        break;
    }
  }

  function showWorkbookResult(result) {
    if (!result) {
      return;
    }

    if (!result.isValid) {
      setBadge(elements.workbookBadge, "Needs attention", "error");
      elements.workbookResult.textContent = (result.errors ?? []).join(" ");
      elements.workbookResult.className = "result error";
      return;
    }

    const counts = Object.fromEntries((result.sheets ?? []).map(sheet => [sheet.name, sheet.dataRowCount]));
    setBadge(elements.workbookBadge, "Ready", "success");
    elements.workbookResult.textContent = `Validated ${counts["Tower Coverage"] ?? 0} tower rows and ${counts["Fibre Coverage"] ?? 0} fibre rows.`;
    elements.workbookResult.className = "result success";
  }

  async function configureGoogle(apiKey) {
    if (!apiKey) {
      showMessage("Enter the company key to run the provider proof.", "error");
      return;
    }

    resetProof();
    showMessage("Loading Google services…");

    try {
      await loadGoogle(apiKey);
      await initializeGoogleProof();
      showMessage("All three Google services responded. The local-provider proof is ready.", "success");
    } catch (error) {
      [elements.mapProof, elements.placesProof, elements.elevationProof]
        .filter(element => !element.classList.contains("pass"))
        .forEach(element => setProof(element, "fail"));
      console.error("Google provider proof failed without exposing the key.", error);
      showMessage("Google proof failed. Confirm the three APIs, billing, and the HTTPS referrer restriction for app.simpleservicechecker.local.", "error");
    }
  }

  function loadGoogle(apiKey) {
    if (globalThis.google?.maps?.importLibrary) {
      return Promise.resolve();
    }

    if (state.googleLoadPromise) {
      return state.googleLoadPromise;
    }

    state.googleLoadPromise = new Promise((resolve, reject) => {
      const callbackName = `__sscGoogleLoaded_${Date.now()}`;
      const script = document.createElement("script");
      const parameters = new URLSearchParams({
        key: apiKey,
        loading: "async",
        libraries: "places",
        callback: callbackName,
        v: "weekly",
      });

      globalThis[callbackName] = () => {
        delete globalThis[callbackName];
        resolve();
      };
      script.onerror = () => {
        delete globalThis[callbackName];
        state.googleLoadPromise = null;
        reject(new Error("Google Maps JavaScript failed to load."));
      };
      script.src = `https://maps.googleapis.com/maps/api/js?${parameters}`;
      document.head.append(script);
    });

    return state.googleLoadPromise;
  }

  async function initializeGoogleProof() {
    const { Map } = await google.maps.importLibrary("maps");
    const center = { lat: 46.49, lng: -84.34 };
    state.map = new Map(document.querySelector("#map"), {
      center,
      zoom: 10,
      mapTypeControl: false,
      streetViewControl: false,
      fullscreenControl: false,
    });

    const subscriber = { lat: 46.51, lng: -84.43 };
    const tower = { lat: 46.43, lng: -84.29 };
    new google.maps.Circle({ map: state.map, center: subscriber, radius: 450, fillColor: "#326d91", fillOpacity: 0.9, strokeColor: "#fffaf2", strokeWeight: 2 });
    new google.maps.Circle({ map: state.map, center: tower, radius: 600, fillColor: "#d66f35", fillOpacity: 0.9, strokeColor: "#fffaf2", strokeWeight: 2 });
    new google.maps.Circle({ map: state.map, center: tower, radius: 18000, fillColor: "#d66f35", fillOpacity: 0.08, strokeColor: "#d66f35", strokeOpacity: 0.4 });
    setProof(elements.mapProof, "pass");

    await initializePlaces();
    await proveElevation(subscriber, tower);
  }

  async function initializePlaces() {
    const { PlaceAutocompleteElement } = await google.maps.importLibrary("places");
    const autocomplete = new PlaceAutocompleteElement({});
    autocomplete.placeholder = "Search an address";
    elements.placeControl.replaceChildren(autocomplete);

    autocomplete.addEventListener("gmp-select", async event => {
      const place = event.placePrediction.toPlace();
      await place.fetchFields({ fields: ["displayName", "formattedAddress", "location"] });
      if (place.location && state.map) {
        state.map.panTo(place.location);
        state.map.setZoom(15);
      }
    });
    setProof(elements.placesProof, "pass");
  }

  async function proveElevation(origin, destination) {
    const elevationService = new google.maps.ElevationService();
    const response = await elevationService.getElevationAlongPath({
      path: [origin, destination],
      samples: 64,
    });
    const samples = Array.isArray(response) ? response : response.results;
    if (!samples || samples.length < 2) {
      throw new Error("Elevation returned no samples.");
    }

    drawClearancePath(samples);
    setProof(elements.elevationProof, "pass");
  }

  function drawClearancePath(samples) {
    const metresPerFoot = 0.3048;
    const cpeHeightMetres = 25 * metresPerFoot;
    const towerHeightMetres = 100 * metresPerFoot;
    const clutterHeightMetres = 50 * metresPerFoot;
    const startLineHeight = samples[0].elevation + cpeHeightMetres;
    const endLineHeight = samples.at(-1).elevation + towerHeightMetres;
    const lastIndex = samples.length - 1;
    const segmentColours = [];

    for (let index = 1; index <= lastIndex; index += 1) {
      const progress = index / lastIndex;
      const lineHeight = startLineHeight + ((endLineHeight - startLineHeight) * progress);
      const clearance = lineHeight - (samples[index].elevation + clutterHeightMetres);
      segmentColours[index] = clearanceColour(clearance);
    }

    let runStart = 0;
    let runColour = segmentColours[1] ?? "#2e8b57";
    for (let index = 2; index <= lastIndex; index += 1) {
      const colour = segmentColours[index] ?? "#2e8b57";
      if (colour === runColour) {
        continue;
      }

      drawClearanceSegment(samples.slice(runStart, index), runColour);
      runStart = index - 1;
      runColour = colour;
    }

    drawClearanceSegment(samples.slice(runStart), runColour);
  }

  function drawClearanceSegment(samples, strokeColor) {
    new google.maps.Polyline({
      map: state.map,
      path: samples.map(sample => sample.location),
      geodesic: true,
      clickable: false,
      strokeColor,
      strokeOpacity: 1,
      strokeWeight: 5,
      zIndex: 90,
    });
  }

  function clearanceColour(clearanceMetres) {
    if (clearanceMetres <= 0) {
      return "#111";
    }
    if (clearanceMetres <= 3.048) {
      return "#d62828";
    }
    if (clearanceMetres <= 9.144) {
      return "#e8842f";
    }
    if (clearanceMetres <= 15.24) {
      return "#d9b51c";
    }
    return "#2e8b57";
  }

  function renderSyntheticChart() {
    if (!globalThis.Chart) {
      return;
    }

    const labels = ["0", "2", "4", "6", "8", "10", "12", "14"];
    const terrain = [184, 191, 204, 197, 215, 207, 199, 188];
    const lineOfSight = [220, 219, 218, 217, 216, 215, 214, 213];
    const fresnelUpper = [220, 226, 230, 232, 231, 227, 221, 213];
    const fresnelLower = [220, 212, 206, 202, 201, 203, 207, 213];

    state.chart = new Chart(document.querySelector("#terrain-chart"), {
      type: "line",
      data: {
        labels,
        datasets: [
          { label: "Terrain", data: terrain, borderColor: "#455b51", backgroundColor: "rgba(69, 91, 81, 0.2)", fill: "origin", tension: 0.35, pointRadius: 0 },
          { label: "Line of sight", data: lineOfSight, borderColor: "#2f6f73", borderWidth: 3, pointRadius: 0 },
          { label: "Fresnel upper", data: fresnelUpper, borderColor: "rgba(64, 132, 123, 0.8)", borderDash: [6, 5], pointRadius: 0 },
          { label: "Fresnel lower", data: fresnelLower, borderColor: "rgba(64, 132, 123, 0.8)", backgroundColor: "rgba(64, 132, 123, 0.12)", borderDash: [6, 5], pointRadius: 0, fill: "-1" },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: false,
        plugins: { legend: { display: false }, tooltip: { enabled: true } },
        scales: {
          x: { title: { display: true, text: "Path distance (km)" }, grid: { display: false } },
          y: { title: { display: true, text: "Elevation (m)" }, suggestedMin: 170 },
        },
      },
    });
  }

  elements.saveKey.addEventListener("click", () => {
    const apiKey = elements.googleKey.value.trim();
    if (!apiKey) {
      setBadge(elements.keyBadge, "Key required", "error");
      return;
    }
    post("saveGoogleKey", { apiKey });
  });
  elements.clearKey.addEventListener("click", () => post("clearGoogleKey"));
  elements.chooseWorkbook.addEventListener("click", () => post("chooseWorkbook"));
  globalThis.chrome?.webview?.addEventListener("message", event => receive(event.data));

  renderSyntheticChart();
  globalThis.simpleServiceChecker = { receive, configureGoogle };
  post("appReady");
})();
