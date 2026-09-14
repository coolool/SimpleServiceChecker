(() => {
  "use strict";

  const bridgeVersion = 1;
  const localOrigin = "https://app.simpleservicechecker.local";
  const state = {
    apiKeyConfigured: false,
    workbookConfigured: false,
    map: null,
    towerData: null,
    towerRanges: [],
    candidatePaths: [],
    customerPin: null,
    infoWindow: null,
    coverage: null,
    googleLoadPromise: null,
    revision: 0,
    fittedCoverage: false,
    firstRunSetup: false,
  };

  const elements = Object.fromEntries([
    "shell", "sidebar", "sidebar-resizer", "settings-toggle", "settings-close", "settings-panel",
    "google-key", "save-key", "clear-key", "key-badge", "provider-result",
    "choose-workbook", "workbook-badge", "workbook-result", "data-summary",
    "place-control", "latitude", "longitude", "check-coordinates", "clear-location",
    "location-status", "fibre-result", "candidate-count", "candidate-list", "map-status",
  ].map(id => [id.replaceAll("-", "_"), document.querySelector(`#${id}`)]));

  function requestId() {
    return globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random()}`;
  }

  function post(type, payload = {}) {
    if (!globalThis.chrome?.webview) {
      setStatus(elements.location_status, "The secure desktop bridge is not available.", "error");
      return;
    }

    globalThis.chrome.webview.postMessage({ version: bridgeVersion, type, requestId: requestId(), payload });
  }

  function setBadge(element, text, kind) {
    element.textContent = text;
    element.className = `badge ${kind}`;
  }

  function setStatus(element, text, kind = "neutral") {
    element.textContent = text;
    element.className = `status-box ${kind}`;
  }

  function setMapStatus(text, kind = "") {
    elements.map_status.className = `map-status ${kind}`;
    elements.map_status.replaceChildren(node("b", { "aria-hidden": "true" }, kind === "ready" ? "●" : kind === "error" ? "!" : "○"), document.createTextNode(text));
  }

  function showSettings(show) {
    elements.settings_panel.hidden = !show;
    elements.settings_toggle.setAttribute("aria-expanded", String(show));
    if (show) {
      elements.settings_close.focus();
    }
  }

  function maybeFinishFirstSetup() {
    if (state.firstRunSetup && state.apiKeyConfigured && state.workbookConfigured && !elements.settings_panel.hidden) {
      state.firstRunSetup = false;
      showSettings(false);
    }
  }

  function receive(message) {
    const envelope = typeof message === "string" ? JSON.parse(message) : message;
    const payload = envelope.payload ?? {};

    switch (envelope.type) {
      case "setupState":
        state.apiKeyConfigured = Boolean(payload.apiKeyConfigured);
        state.workbookConfigured = Boolean(payload.workbookConfigured);
        state.firstRunSetup = !state.apiKeyConfigured || !state.workbookConfigured;
        setBadge(elements.key_badge, state.apiKeyConfigured ? "Saved locally" : "Not configured", state.apiKeyConfigured ? "success" : "neutral");
        setBadge(elements.workbook_badge, state.workbookConfigured ? "Ready" : "Not configured", state.workbookConfigured ? "success" : "neutral");
        if (!state.apiKeyConfigured || !state.workbookConfigured) {
          showSettings(true);
        }
        if (globalThis.location.origin !== localOrigin) {
          setStatus(elements.provider_result, "The application origin is not the approved local HTTPS origin.", "error");
        }
        break;
      case "coverageLoaded":
        acceptCoverage(payload);
        break;
      case "workbookRejected":
        setBadge(elements.workbook_badge, state.workbookConfigured ? "Active retained" : "Needs attention", state.workbookConfigured ? "success" : "error");
        setStatus(elements.workbook_result, `${(payload.errors ?? ["The workbook is not valid."]).join(" ")} ${state.workbookConfigured ? "The current working copy remains active." : ""}`.trim(), "error");
        break;
      case "workbookUnavailable":
        state.workbookConfigured = false;
        setBadge(elements.workbook_badge, "Unavailable", "error");
        setStatus(elements.workbook_result, payload.message ?? "The saved workbook is unavailable.", "error");
        showSettings(true);
        break;
      case "workbookSelectionCancelled":
        break;
      case "googleKeyRejected":
        setBadge(elements.key_badge, "Check key", "error");
        setStatus(elements.provider_result, payload.message ?? "The key could not be saved.", "error");
        break;
      case "locationResult":
        if (payload.revision === state.revision) {
          renderLocationResult(payload);
        }
        break;
      case "locationRejected":
        setStatus(elements.location_status, payload.message ?? "That location could not be checked.", "error");
        break;
      case "bridgeError":
      case "operationFailed":
        setStatus(elements.location_status, payload.message ?? "An unexpected application error occurred.", "error");
        break;
      default:
        break;
    }
  }

  function acceptCoverage(payload) {
    state.coverage = payload;
    state.workbookConfigured = true;
    state.fittedCoverage = false;
    setBadge(elements.workbook_badge, "Ready", "success");
    const warningText = payload.warningCount === 1 ? "1 non-blocking warning" : `${payload.warningCount ?? 0} non-blocking warnings`;
    setStatus(elements.workbook_result, `${payload.fileName}: ${payload.towerCount} towers and ${payload.fibreRangeCount} fibre ranges; ${warningText}.`, payload.warningCount ? "warning" : "success");
    elements.data_summary.textContent = `${payload.fileName} · ${payload.towerCount} towers · ${payload.fibreRangeCount} fibre ranges`;
    renderTowerCoverage();
    maybeFinishFirstSetup();
  }

  async function configureGoogle(apiKey) {
    if (!apiKey) {
      setStatus(elements.provider_result, "Enter the company API key to enable the map and address search.", "error");
      return;
    }

    setStatus(elements.provider_result, "Loading Google Maps and Places…");
    setMapStatus("Loading Google Maps");
    globalThis.gm_authFailure = () => {
      state.apiKeyConfigured = false;
      setBadge(elements.key_badge, "Provider rejected", "error");
      setStatus(elements.provider_result, "Google rejected this key. Check billing, API restrictions, quota, and the application restriction.", "error");
      setMapStatus("Google key or quota error", "error");
    };

    try {
      await loadGoogle(apiKey);
      await initializeMap();
      await initializePlaces();
      state.apiKeyConfigured = true;
      setBadge(elements.key_badge, "Connected", "success");
      setStatus(elements.provider_result, "Maps and Places are ready. Elevation is used only when a wireless profile is opened.", "success");
      setMapStatus(state.coverage ? `${state.coverage.towerCount} towers loaded` : "Map ready—choose a workbook", "ready");
      maybeFinishFirstSetup();
    } catch (error) {
      console.error("Google setup failed without exposing the key.", error);
      setBadge(elements.key_badge, "Provider error", "error");
      setStatus(elements.provider_result, navigator.onLine
        ? "Google could not start. Check enabled APIs, billing, quota, and the application restriction."
        : "This computer is offline. Coordinate checks remain available when a workbook is loaded.", "error");
      setMapStatus(navigator.onLine ? "Google provider unavailable" : "Offline—local checks still work", "error");
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
      globalThis[callbackName] = () => { delete globalThis[callbackName]; resolve(); };
      script.onerror = () => { delete globalThis[callbackName]; state.googleLoadPromise = null; reject(new Error("Google Maps JavaScript failed to load.")); };
      script.src = `https://maps.googleapis.com/maps/api/js?${new URLSearchParams({ key: apiKey, loading: "async", libraries: "places", callback: callbackName, v: "weekly" })}`;
      document.head.append(script);
    });
    return state.googleLoadPromise;
  }

  async function initializeMap() {
    const { Map } = await google.maps.importLibrary("maps");
    state.map = new Map(document.querySelector("#map"), {
      center: { lat: 46.51, lng: -84.34 },
      zoom: 10,
      mapTypeControl: false,
      streetViewControl: false,
      fullscreenControl: false,
      clickableIcons: false,
    });
    state.infoWindow = new google.maps.InfoWindow();
    state.map.addListener("click", event => {
      if (event.latLng) {
        selectLocation(event.latLng.toJSON(), "map", null);
      }
    });
    renderTowerCoverage();
  }

  async function initializePlaces() {
    const { PlaceAutocompleteElement } = await google.maps.importLibrary("places");
    const autocomplete = new PlaceAutocompleteElement({});
    autocomplete.placeholder = "Search a customer address";
    elements.place_control.replaceChildren(autocomplete);
    autocomplete.addEventListener("gmp-select", async event => {
      try {
        setStatus(elements.location_status, "Reading the selected address…");
        const place = event.placePrediction.toPlace();
        await place.fetchFields({ fields: ["displayName", "formattedAddress", "location", "viewport", "addressComponents"] });
        if (!place.location) {
          throw new Error("The selected place has no coordinates.");
        }
        if (place.viewport && state.map) {
          state.map.fitBounds(place.viewport);
        } else {
          state.map?.panTo(place.location);
          state.map?.setZoom(16);
        }
        selectLocation(place.location.toJSON(), "places", addressFromPlace(place));
      } catch (error) {
        console.error("Place selection failed.", error);
        setStatus(elements.location_status, "Google could not read that address. Try coordinates or click the map.", "error");
      }
    });
  }

  function addressFromPlace(place) {
    const components = place.addressComponents ?? [];
    const find = (...types) => components.find(component => types.some(type => component.types?.includes(type)))?.longText ?? null;
    return {
      civicNumber: find("street_number"),
      route: find("route"),
      city: find("locality", "postal_town", "administrative_area_level_3"),
      postalCode: find("postal_code"),
      formattedAddress: place.formattedAddress ?? place.displayName ?? null,
    };
  }

  function renderTowerCoverage() {
    if (!state.map || !state.coverage?.towers) {
      return;
    }

    state.towerRanges.forEach(circle => circle.setMap(null));
    state.towerRanges = [];
    state.towerData?.setMap(null);
    state.towerData = new google.maps.Data({ map: state.map });
    const byRow = new Map(state.coverage.towers.map(tower => [tower.rowNumber, tower]));
    const bounds = new google.maps.LatLngBounds();

    for (const tower of state.coverage.towers) {
      const center = { lat: tower.latitude, lng: tower.longitude };
      bounds.extend(center);
      state.towerData.add({ geometry: new google.maps.Data.Point(center), properties: { rowNumber: tower.rowNumber } });
      if (tower.candidateEnabled && tower.rangeMetres > 0) {
        state.towerRanges.push(new google.maps.Circle({
          map: state.map,
          center,
          radius: tower.rangeMetres,
          clickable: false,
          fillColor: "#b75e3d",
          fillOpacity: .025,
          strokeColor: "#b75e3d",
          strokeOpacity: .2,
          strokeWeight: 1,
          zIndex: 1,
        }));
      }
    }

    state.towerData.setStyle(feature => {
      const tower = byRow.get(feature.getProperty("rowNumber"));
      return {
        icon: {
          path: google.maps.SymbolPath.CIRCLE,
          scale: tower?.candidateEnabled ? 6 : 5,
          fillColor: tower?.candidateEnabled ? "#b75e3d" : "#77706a",
          fillOpacity: 1,
          strokeColor: "#fffaf2",
          strokeWeight: 2,
        },
        title: tower?.name ?? "Tower",
        zIndex: 20,
      };
    });
    state.towerData.addListener("click", event => {
      const tower = byRow.get(event.feature.getProperty("rowNumber"));
      if (!tower || !event.latLng) return;
      const content = node("div", { class: "tower-popup" },
        node("strong", {}, tower.name),
        node("span", {}, `${formatNumber(tower.rangeMetres / 1000, 1)} km range · ${formatNumber(tower.maxDownloadMbps, 0)}/${formatNumber(tower.maxUploadMbps, 0)} Mbps`));
      state.infoWindow.setContent(content);
      state.infoWindow.setPosition(event.latLng);
      state.infoWindow.open({ map: state.map });
    });

    if (!state.fittedCoverage && !bounds.isEmpty()) {
      state.map.fitBounds(bounds, 42);
      state.fittedCoverage = true;
    }
    setMapStatus(`${state.coverage.towerCount} towers loaded`, "ready");
  }

  function selectLocation(position, source, address) {
    const latitude = Number(position.lat);
    const longitude = Number(position.lng);
    if (!Number.isFinite(latitude) || !Number.isFinite(longitude) || Math.abs(latitude) > 90 || Math.abs(longitude) > 180) {
      setStatus(elements.location_status, "Enter valid decimal latitude and longitude values.", "error");
      return;
    }

    state.revision += 1;
    elements.latitude.value = latitude.toFixed(6);
    elements.longitude.value = longitude.toFixed(6);
    elements.clear_location.disabled = false;
    setCustomerPin({ lat: latitude, lng: longitude });
    clearCandidatePaths();
    showLoadingResults();
    post("selectLocation", { revision: state.revision, latitude, longitude, source, address });
  }

  function setCustomerPin(position) {
    if (!state.map) return;
    if (!state.customerPin) {
      state.customerPin = new google.maps.Circle({
        map: state.map,
        center: position,
        radius: 110,
        draggable: true,
        fillColor: "#326d91",
        fillOpacity: .95,
        strokeColor: "#ffffff",
        strokeWeight: 3,
        zIndex: 100,
      });
      state.customerPin.addListener("dragend", () => {
        const center = state.customerPin.getCenter();
        if (center) selectLocation(center.toJSON(), "drag", null);
      });
    } else {
      state.customerPin.setCenter(position);
    }
  }

  function showLoadingResults() {
    setStatus(elements.location_status, "Checking the active workbook locally…");
    elements.fibre_result.className = "availability empty";
    elements.fibre_result.replaceChildren(node("strong", {}, "Checking fibre…"), node("span", {}, "Looking for an exact civic-address match."));
    elements.candidate_count.textContent = "…";
    elements.candidate_list.replaceChildren(node("div", { class: "empty-card" }, "Calculating tower distances and published coverage…"));
  }

  function renderLocationResult(result) {
    const locationText = result.formattedAddress || `${Number(result.latitude).toFixed(6)}, ${Number(result.longitude).toFixed(6)}`;
    setStatus(elements.location_status, `Showing local results for ${locationText}`, "success");
    renderFibre(result.fibre ?? {});
    renderCandidates(result.candidates ?? [], { lat: result.latitude, lng: result.longitude });
  }

  function renderFibre(fibre) {
    const content = {
      available: ["Available in workbook", fibre.maxSpeeds ? `Maximum listed speed: ${fibre.maxSpeeds}` : "A matching fibre range was found."],
      notlisted: ["Not listed", "The active workbook contains no fibre match for this civic address."],
      addressrequired: ["Address required", "Coordinates alone cannot be used for fibre matching. Select a complete Google address."],
      dataerror: ["Data error", "The active workbook could not produce a fibre result."],
    }[fibre.status] ?? ["Result unavailable", "The fibre result was not recognized."];
    const className = { available: "available", notlisted: "not-listed", addressrequired: "address-required", dataerror: "data-error" }[fibre.status] ?? "data-error";
    elements.fibre_result.className = `availability ${className}`;
    elements.fibre_result.replaceChildren(node("strong", {}, content[0]), node("span", {}, content[1]));
    if (fibre.notes) elements.fibre_result.append(node("span", {}, `Note: ${fibre.notes}`));
    for (const warning of fibre.warnings ?? []) elements.fibre_result.append(node("span", {}, `Warning: ${warning}`));
  }

  function renderCandidates(candidates, customer) {
    elements.candidate_count.textContent = String(candidates.length);
    elements.candidate_list.replaceChildren();
    clearCandidatePaths();
    if (!candidates.length) {
      elements.candidate_list.append(node("div", { class: "empty-card" }, "No towers include this location within their published range."));
      return;
    }

    candidates.forEach((candidate, index) => {
      const card = node("article", { class: "candidate-card" },
        node("header", {}, node("h3", {}, `${index + 1}. ${candidate.name}`), node("span", { class: "distance" }, `${formatNumber(candidate.distanceKilometres, 1)} km`)),
        node("dl", { class: "candidate-facts" },
          fact("Maximum speed", `${formatNumber(candidate.maxDownloadMbps, 0)} down / ${formatNumber(candidate.maxUploadMbps, 0)} up Mbps`),
          fact("Tower height", `${formatNumber(candidate.heightFeet, 0)} ft`),
          fact("Published range", `${formatNumber(candidate.rangeKilometres, 1)} km`),
          fact("Profile", "Ready in Package 4")),
        candidate.notes ? node("p", { class: "candidate-notes" }, candidate.notes) : null,
        node("span", { class: "plot-state" }, "Distance and range match only—terrain clearance not yet calculated."));
      elements.candidate_list.append(card);

      if (state.map) {
        state.candidatePaths.push(new google.maps.Polyline({
          map: state.map,
          path: [customer, { lat: candidate.latitude, lng: candidate.longitude }],
          geodesic: true,
          clickable: false,
          strokeColor: "#3f2d23",
          strokeOpacity: .8,
          strokeWeight: index === 0 ? 4 : 3,
          zIndex: 60 - index,
        }));
      }
    });
  }

  function fact(label, value) {
    return node("div", {}, node("dt", {}, label), node("dd", {}, value));
  }

  function clearCandidatePaths() {
    state.candidatePaths.forEach(path => path.setMap(null));
    state.candidatePaths = [];
  }

  function clearLocation() {
    state.revision += 1;
    post("clearLocation");
    elements.latitude.value = "";
    elements.longitude.value = "";
    elements.clear_location.disabled = true;
    state.customerPin?.setMap(null);
    state.customerPin = null;
    clearCandidatePaths();
    setStatus(elements.location_status, "Choose an address, enter coordinates, or click the map.");
    elements.fibre_result.className = "availability empty";
    elements.fibre_result.replaceChildren(node("strong", {}, "No location selected"), node("span", {}, "The active workbook will be checked for an exact civic-address match."));
    elements.candidate_count.textContent = "—";
    elements.candidate_list.replaceChildren(node("div", { class: "empty-card" }, "Select a customer location to see nearby towers."));
  }

  function node(tag, attributes = {}, ...children) {
    const element = document.createElement(tag);
    for (const [name, value] of Object.entries(attributes)) element.setAttribute(name, value);
    for (const child of children.flat()) if (child !== null && child !== undefined) element.append(child instanceof Node ? child : document.createTextNode(String(child)));
    return element;
  }

  function formatNumber(value, digits) {
    return Number(value).toLocaleString(undefined, { maximumFractionDigits: digits, minimumFractionDigits: digits });
  }

  function initializeResizer() {
    let startX = 0;
    let startWidth = 0;
    const applyWidth = width => document.documentElement.style.setProperty("--sidebar-width", `${Math.max(320, Math.min(width, Math.min(540, globalThis.innerWidth * .55)))}px`);
    elements.sidebar_resizer.addEventListener("pointerdown", event => {
      startX = event.clientX;
      startWidth = elements.sidebar.getBoundingClientRect().width;
      elements.sidebar_resizer.setPointerCapture(event.pointerId);
    });
    elements.sidebar_resizer.addEventListener("pointermove", event => {
      if (elements.sidebar_resizer.hasPointerCapture(event.pointerId)) applyWidth(startWidth + event.clientX - startX);
    });
    elements.sidebar_resizer.addEventListener("pointerup", event => {
      if (elements.sidebar_resizer.hasPointerCapture(event.pointerId)) elements.sidebar_resizer.releasePointerCapture(event.pointerId);
      if (state.map) google.maps.event.trigger(state.map, "resize");
    });
    elements.sidebar_resizer.addEventListener("keydown", event => {
      if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") return;
      event.preventDefault();
      applyWidth(elements.sidebar.getBoundingClientRect().width + (event.key === "ArrowRight" ? 20 : -20));
      if (state.map) google.maps.event.trigger(state.map, "resize");
    });
  }

  elements.settings_toggle.addEventListener("click", () => showSettings(elements.settings_panel.hidden));
  elements.settings_close.addEventListener("click", () => showSettings(false));
  elements.save_key.addEventListener("click", () => {
    const apiKey = elements.google_key.value.trim();
    if (!apiKey) {
      setBadge(elements.key_badge, "Key required", "error");
      return;
    }
    post("saveGoogleKey", { apiKey });
  });
  elements.clear_key.addEventListener("click", () => post("clearGoogleKey"));
  elements.choose_workbook.addEventListener("click", () => post("chooseWorkbook"));
  elements.check_coordinates.addEventListener("click", () => selectLocation({ lat: elements.latitude.value, lng: elements.longitude.value }, "coordinates", null));
  [elements.latitude, elements.longitude].forEach(input => input.addEventListener("keydown", event => {
    if (event.key === "Enter") elements.check_coordinates.click();
  }));
  elements.clear_location.addEventListener("click", clearLocation);
  globalThis.addEventListener("offline", () => {
    setMapStatus("Offline—local coordinate checks still work", "error");
    setStatus(elements.provider_result, "This computer is offline. Existing coordinates can still be checked locally.", "warning");
  });
  globalThis.addEventListener("online", () => {
    if (state.map) setMapStatus(state.coverage ? `${state.coverage.towerCount} towers loaded` : "Map ready", "ready");
  });
  globalThis.chrome?.webview?.addEventListener("message", event => receive(event.data));

  initializeResizer();
  globalThis.simpleServiceChecker = { receive, configureGoogle };
  post("appReady");
})();
