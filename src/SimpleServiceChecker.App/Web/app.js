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
    towerLabels: [],
    candidatePaths: [],
    customerPin: null,
    infoWindow: null,
    coverage: null,
    googleLoadPromise: null,
    revision: 0,
    fittedCoverage: false,
    firstRunSetup: false,
    elevationService: null,
    geocoder: null,
    reverseGeocodeRevision: 0,
    locationNotice: null,
    elevationBusy: false,
    plotRevision: 0,
    selectedCandidate: null,
    customerLocation: null,
    lastCandidates: [],
    terrainSamples: null,
    chart: null,
  };

  const elements = Object.fromEntries([
    "shell", "sidebar", "sidebar-resizer", "settings-toggle", "settings-close", "settings-panel",
    "google-key", "save-key", "clear-key", "key-badge", "provider-result",
    "choose-workbook", "workbook-badge", "workbook-result", "data-summary",
    "place-control", "latitude", "longitude", "check-coordinates", "clear-location",
    "location-status", "fibre-result", "candidate-count", "candidate-list", "map-status",
    "clearance-legend", "profile-card", "profile-heading", "profile-subtitle", "profile-close",
    "customer-height", "tower-height", "clutter-height", "frequency", "fresnel-reference",
    "earth-model", "profile-refresh", "profile-status", "profile-output", "profile-summary", "terrain-chart",
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
      case "terrainProfileResult":
        if (payload.locationRevision === state.revision && payload.plotRevision === state.plotRevision) {
          renderTerrainProfile(payload);
        }
        break;
      case "terrainProfileRejected":
        if (!payload.plotRevision || payload.plotRevision === state.plotRevision) {
          showProfileStatus(payload.message ?? "No clearance assessment was made.", "error");
          elements.profile_output.hidden = true;
        }
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
      await initializeElevation();
      await initializeGeocoder();
      state.apiKeyConfigured = true;
      setBadge(elements.key_badge, "Connected", "success");
      setStatus(elements.provider_result, "Maps, Places, and pin address lookup are ready. Elevation is used only when a wireless profile is opened.", "success");
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
        reverseGeocodeAndSelect(event.latLng.toJSON(), "map");
      }
    });
    state.map.addListener("zoom_changed", updateCustomerPinSize);
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

  async function initializeElevation() {
    const { ElevationService } = await google.maps.importLibrary("elevation");
    state.elevationService = new ElevationService();
  }

  async function initializeGeocoder() {
    const { Geocoder } = await google.maps.importLibrary("geocoding");
    state.geocoder = new Geocoder();
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

  function addressFromGeocoderResult(result) {
    const components = result?.address_components ?? [];
    const find = (...types) => components.find(component => types.some(type => component.types?.includes(type)))?.long_name ?? null;
    return {
      civicNumber: find("street_number"),
      route: find("route"),
      city: find("locality", "postal_town", "administrative_area_level_3"),
      postalCode: find("postal_code"),
      formattedAddress: result?.formatted_address ?? null,
    };
  }

  async function reverseGeocodeAndSelect(position, source) {
    const latitude = Number(position.lat);
    const longitude = Number(position.lng);
    const geocodeRevision = ++state.reverseGeocodeRevision;
    if (!state.geocoder) {
      selectLocation({ lat: latitude, lng: longitude }, source, null,
        "Google address lookup is unavailable, so fibre could only be checked by coordinates.");
      return;
    }

    setCustomerPin({ lat: latitude, lng: longitude });
    elements.latitude.value = latitude.toFixed(6);
    elements.longitude.value = longitude.toFixed(6);
    setStatus(elements.location_status, "Finding the closest civic address for this pin…");
    try {
      const response = await state.geocoder.geocode({ location: { lat: latitude, lng: longitude } });
      if (geocodeRevision !== state.reverseGeocodeRevision) return;
      const results = response.results ?? [];
      const completeResult = results.find(item => {
        const address = addressFromGeocoderResult(item);
        return address.civicNumber && address.route && address.city;
      });
      const result = completeResult ?? results.find(item => {
        const address = addressFromGeocoderResult(item);
        return address.civicNumber && address.route;
      }) ?? results[0];
      const address = result ? addressFromGeocoderResult(result) : null;
      const hasCivicAddress = Boolean(address?.civicNumber && address?.route && address?.city);
      selectLocation({ lat: latitude, lng: longitude }, `${source}-geocode`, address,
        hasCivicAddress
          ? "Fibre used Google's closest civic-address estimate for the dropped pin."
          : "Google did not return a complete civic address, so fibre could not be matched.");
    } catch (error) {
      console.error("Reverse geocoding failed.", error);
      if (geocodeRevision !== state.reverseGeocodeRevision) return;
      selectLocation({ lat: latitude, lng: longitude }, source, null,
        "Google could not estimate an address. Enable the Geocoding API or search for the address directly.");
    }
  }

  function renderTowerCoverage() {
    if (!state.map || !state.coverage?.towers) {
      return;
    }

    state.towerRanges.forEach(circle => circle.setMap(null));
    state.towerRanges = [];
    state.towerLabels.forEach(label => label.setMap(null));
    state.towerLabels = [];
    state.towerData?.setMap(null);
    state.towerData = new google.maps.Data({ map: state.map });
    const byRow = new Map(state.coverage.towers.map(tower => [tower.rowNumber, tower]));
    const bounds = new google.maps.LatLngBounds();

    for (const tower of state.coverage.towers) {
      const center = { lat: tower.latitude, lng: tower.longitude };
      bounds.extend(center);
      state.towerData.add({ geometry: new google.maps.Data.Point(center), properties: { rowNumber: tower.rowNumber } });
      state.towerLabels.push(createTowerLabel(center, tower.name));
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

  function createTowerLabel(position, text) {
    const overlay = new google.maps.OverlayView();
    overlay.onAdd = function () {
      this.labelElement = node("div", { class: "tower-map-label" }, text);
      this.getPanes().overlayMouseTarget.append(this.labelElement);
    };
    overlay.draw = function () {
      if (!this.labelElement) return;
      const pixel = this.getProjection().fromLatLngToDivPixel(new google.maps.LatLng(position));
      const zoom = this.getMap()?.getZoom() ?? 0;
      this.labelElement.hidden = zoom < 10;
      this.labelElement.style.left = `${pixel.x}px`;
      this.labelElement.style.top = `${pixel.y}px`;
    };
    overlay.onRemove = function () {
      this.labelElement?.remove();
      this.labelElement = null;
    };
    overlay.setMap(state.map);
    return overlay;
  }

  function selectLocation(position, source, address, notice = null) {
    const latitude = Number(position.lat);
    const longitude = Number(position.lng);
    if (!Number.isFinite(latitude) || !Number.isFinite(longitude) || Math.abs(latitude) > 90 || Math.abs(longitude) > 180) {
      setStatus(elements.location_status, "Enter valid decimal latitude and longitude values.", "error");
      return;
    }

    state.revision += 1;
    state.reverseGeocodeRevision += 1;
    state.locationNotice = notice;
    closeProfile();
    state.customerLocation = { lat: latitude, lng: longitude };
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
        radius: customerPinRadiusMetres(position.lat, state.map.getZoom()),
        draggable: true,
        fillColor: "#326d91",
        fillOpacity: .95,
        strokeColor: "#ffffff",
        strokeWeight: 3,
        zIndex: 100,
      });
      state.customerPin.addListener("dragend", () => {
        const center = state.customerPin.getCenter();
        if (center) reverseGeocodeAndSelect(center.toJSON(), "drag");
      });
    } else {
      state.customerPin.setCenter(position);
      updateCustomerPinSize();
    }
  }

  function customerPinRadiusMetres(latitude, zoom) {
    const metresPerPixel = 156543.03392 * Math.cos(Number(latitude) * Math.PI / 180) / (2 ** Math.max(0, Number(zoom) || 0));
    return Math.max(2, metresPerPixel * 8);
  }

  function updateCustomerPinSize() {
    const center = state.customerPin?.getCenter();
    if (!center || !state.map) return;
    state.customerPin.setRadius(customerPinRadiusMetres(center.lat(), state.map.getZoom()));
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
    setStatus(elements.location_status, `Showing local results for ${locationText}${state.locationNotice ? ` ${state.locationNotice}` : ""}`, "success");
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
    state.lastCandidates = candidates;
    state.customerLocation = customer;
    elements.candidate_count.textContent = String(candidates.length);
    elements.candidate_list.replaceChildren();
    drawCandidatePaths(candidates, customer);
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
          fact("Profile", candidate.terrainSampleCount > 0 ? `${candidate.terrainSampleCount} elevation samples` : "Path unsupported")),
        candidate.notes ? node("p", { class: "candidate-notes" }, candidate.notes) : null,
        node("span", { class: "plot-state" }, "Terrain is requested only when you open this profile."),
        node("button", { class: "plot-button", type: "button", ...(candidate.terrainSampleCount > 0 ? {} : { disabled: "" }) }, candidate.terrainSampleCount > 0 ? "Plot terrain & Fresnel" : "Profile unavailable for this path"));
      card.querySelector(".plot-button").addEventListener("click", () => openProfile(candidate));
      elements.candidate_list.append(card);
    });
  }

  function drawCandidatePaths(candidates, customer, excludedTowerRow = null) {
    clearCandidatePaths();
    if (!state.map || !customer) return;
    candidates.forEach((candidate, index) => {
      if (candidate.rowNumber === excludedTowerRow) return;
      state.candidatePaths.push(new google.maps.Polyline({
        map: state.map,
        path: [customer, { lat: candidate.latitude, lng: candidate.longitude }],
        geodesic: true,
        clickable: false,
        strokeColor: "#3f2d23",
        strokeOpacity: .72,
        strokeWeight: index === 0 ? 4 : 3,
        zIndex: 60 - index,
      }));
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
    closeProfile();
    post("clearLocation");
    elements.latitude.value = "";
    elements.longitude.value = "";
    elements.clear_location.disabled = true;
    state.customerPin?.setMap(null);
    state.customerPin = null;
    state.customerLocation = null;
    state.locationNotice = null;
    state.reverseGeocodeRevision += 1;
    state.lastCandidates = [];
    clearCandidatePaths();
    setStatus(elements.location_status, "Choose an address, enter coordinates, or click the map.");
    elements.fibre_result.className = "availability empty";
    elements.fibre_result.replaceChildren(node("strong", {}, "No location selected"), node("span", {}, "The active workbook will be checked for an exact civic-address match."));
    elements.candidate_count.textContent = "—";
    elements.candidate_list.replaceChildren(node("div", { class: "empty-card" }, "Select a customer location to see nearby towers."));
  }

  function openProfile(candidate) {
    state.plotRevision += 1;
    state.selectedCandidate = candidate;
    state.terrainSamples = null;
    elements.profile_card.hidden = false;
    elements.profile_card.parentElement.classList.add("profile-open");
    elements.profile_heading.textContent = "Terrain and Fresnel clearance";
    elements.profile_subtitle.textContent = `${candidate.name} · ${formatNumber(candidate.distanceKilometres, 2)} km path`;
    elements.tower_height.value = String(candidate.heightFeet);
    elements.profile_output.hidden = true;
    elements.clearance_legend.hidden = true;
    state.chart?.destroy();
    state.chart = null;
    requestElevation();
  }

  function closeProfile() {
    state.plotRevision += 1;
    state.selectedCandidate = null;
    state.terrainSamples = null;
    elements.profile_card.hidden = true;
    elements.profile_card.parentElement.classList.remove("profile-open");
    elements.clearance_legend.hidden = true;
    elements.profile_output.hidden = true;
    state.chart?.destroy();
    state.chart = null;
    drawCandidatePaths(state.lastCandidates, state.customerLocation);
  }

  function showProfileStatus(message, kind = "") {
    elements.profile_status.textContent = message;
    elements.profile_status.className = `profile-status ${kind}`;
  }

  function setProfileBusy(busy) {
    state.elevationBusy = busy;
    elements.profile_refresh.disabled = busy;
    [elements.customer_height, elements.tower_height, elements.clutter_height, elements.frequency, elements.fresnel_reference, elements.earth_model]
      .forEach(control => { control.disabled = busy; });
  }

  function readProfileOptions() {
    const values = {
      customerHeightFeet: Number(elements.customer_height.value),
      towerHeightFeet: Number(elements.tower_height.value),
      clutterHeightFeet: Number(elements.clutter_height.value),
      frequencyGigahertz: Number(elements.frequency.value),
      fresnelClearancePercent: Number(elements.fresnel_reference.value),
      earthModel: elements.earth_model.value,
    };
    const valid = Object.values(values).every(value => typeof value === "string" || Number.isFinite(value)) &&
      values.customerHeightFeet >= 0 && values.customerHeightFeet <= 32808.4 &&
      values.towerHeightFeet >= 0 && values.towerHeightFeet <= 32808.4 &&
      values.clutterHeightFeet >= 0 && values.clutterHeightFeet <= 300 &&
      values.frequencyGigahertz >= .001 && values.frequencyGigahertz <= 1000 &&
      values.fresnelClearancePercent >= 0 && values.fresnelClearancePercent <= 100 &&
      ["standard", "geometric", "flat"].includes(values.earthModel);
    if (!valid) {
      throw new Error("Use values within the ranges shown for height, clutter, frequency, Fresnel reference, and Earth model.");
    }
    return values;
  }

  function requestElevation() {
    const candidate = state.selectedCandidate;
    if (!candidate) return;
    if (!state.elevationService || !state.map) {
      showProfileStatus("Elevation is unavailable. Check Google setup or connectivity; local candidate results remain valid.", "error");
      return;
    }
    if (state.elevationBusy) {
      showProfileStatus("The previous Google elevation request is still settling. Wait, then refresh this profile.", "error");
      return;
    }

    try {
      readProfileOptions();
    } catch (error) {
      showProfileStatus(error.message, "error");
      return;
    }

    const locationRevision = state.revision;
    const plotRevision = state.plotRevision;
    const expectedCount = candidate.terrainSampleCount;
    let settled = false;
    let expired = false;
    state.terrainSamples = null;
    elements.profile_output.hidden = true;
    setProfileBusy(true);
    showProfileStatus(`Requesting ${expectedCount} sampled elevations from Google…`);

    const timeout = globalThis.setTimeout(() => {
      expired = true;
      if (plotRevision === state.plotRevision) {
        showProfileStatus("Elevation timed out. No assessment was made. The sent request may still incur usage and must settle before retrying.", "error");
      }
    }, 15000);

    const settle = (results, status) => {
      if (settled) return;
      settled = true;
      globalThis.clearTimeout(timeout);
      setProfileBusy(false);
      if (expired) {
        if (plotRevision === state.plotRevision) showProfileStatus("The timed-out request has settled. Select Refresh elevation to try again.", "error");
        return;
      }
      if (plotRevision !== state.plotRevision || locationRevision !== state.revision || candidate.rowNumber !== state.selectedCandidate?.rowNumber) {
        if (!elements.profile_card.hidden && state.selectedCandidate) showProfileStatus("The previous elevation request has settled. Select Refresh elevation for this tower.");
        return;
      }
      if (status !== "OK" || !Array.isArray(results) || results.length !== expectedCount) {
        showProfileStatus(elevationFailureMessage(status), "error");
        return;
      }

      try {
        state.terrainSamples = results.map(result => ({
          latitude: typeof result.location?.lat === "function" ? result.location.lat() : result.location?.lat,
          longitude: typeof result.location?.lng === "function" ? result.location.lng() : result.location?.lng,
          elevationMetres: result.elevation,
          resolutionMetres: Number.isFinite(result.resolution) ? result.resolution : null,
        }));
        analyzeCachedTerrain();
      } catch {
        state.terrainSamples = null;
        showProfileStatus("Google returned incomplete elevation data. No clearance assessment was made.", "error");
      }
    };

    try {
      const operation = state.elevationService.getElevationAlongPath({
        path: [state.customerLocation, { lat: candidate.latitude, lng: candidate.longitude }],
        samples: expectedCount,
      }, settle);
      operation?.catch(() => settle(null, "UNKNOWN_ERROR"));
    } catch {
      settle(null, "UNKNOWN_ERROR");
    }
  }

  function elevationFailureMessage(status) {
    return {
      OVER_QUERY_LIMIT: "Elevation quota or rate limit was reached. No clearance assessment was made.",
      OVER_DAILY_LIMIT: "Google billing, key, or daily usage limits prevented elevation. No assessment was made.",
      REQUEST_DENIED: "Google denied Elevation access. Check the enabled API, key restrictions, and billing.",
      INVALID_REQUEST: "Google rejected this elevation path. No assessment was made.",
      DATA_NOT_AVAILABLE: "Google has no elevation data for this path. No assessment was made.",
    }[status] ?? "Google Elevation is unavailable. No clearance assessment was made.";
  }

  function analyzeCachedTerrain() {
    if (!state.terrainSamples || !state.selectedCandidate) return;
    let options;
    try {
      options = readProfileOptions();
    } catch (error) {
      elements.profile_output.hidden = true;
      showProfileStatus(error.message, "error");
      return;
    }

    showProfileStatus("Calculating line of sight, curvature, clutter, and Fresnel clearance locally…");
    post("analyzeTerrain", {
      locationRevision: state.revision,
      plotRevision: state.plotRevision,
      towerRowNumber: state.selectedCandidate.rowNumber,
      ...options,
      samples: state.terrainSamples,
    });
  }

  function renderTerrainProfile(profile) {
    elements.profile_output.hidden = false;
    elements.clearance_legend.hidden = false;
    const lineStatus = profile.isLineOfSightClear ? "Sampled line of sight clear" : "Sampled line of sight obstructed";
    const fresnelStatus = profile.isFresnelReferenceClear ? `${profile.fresnelClearancePercent}% Fresnel reference clear` : `${profile.fresnelClearancePercent}% Fresnel reference obstructed`;
    showProfileStatus(`${lineStatus}; ${fresnelStatus}.`, profile.isLineOfSightClear && profile.isFresnelReferenceClear ? "success" : "warning");
    const summary = profileSummary(profile);
    elements.profile_summary.replaceChildren(...summary.map((item, index) =>
      node("span", { ...(index === summary.length - 1 ? { class: "profile-disclaimer" } : {}) }, item)));
    renderTerrainChart(profile);
    renderClearancePath(profile);
  }

  function profileSummary(profile) {
    const earthModel = { standard: "4/3 effective Earth radius", geometric: "geometric Earth (k=1)", flat: "flat-earth comparison" }[profile.earthModel] ?? profile.earthModel;
    const resolution = profile.maximumResolutionMetres == null ? "unknown" : `up to ${formatNumber(profile.maximumResolutionMetres, 1)} m`;
    return [
      `Distance: ${formatNumber(profile.distanceMetres / 1000, 3)} km · Bearing: ${formatNumber(profile.bearingDegrees, 1)}° true`,
      `Minimum line clearance: ${formatNumber(profile.minimumClearanceMetres, 2)} m at ${formatNumber(profile.minimumClearanceDistanceMetres / 1000, 3)} km`,
      `Minimum ${formatNumber(profile.fresnelClearancePercent, 0)}% Fresnel margin: ${formatNumber(profile.minimumFresnelMarginMetres, 2)} m at ${formatNumber(profile.minimumFresnelMarginDistanceMetres / 1000, 3)} km`,
      `Assumptions: ${formatNumber(profile.frequencyGigahertz, 2)} GHz · ${earthModel} · ${formatNumber(profile.clutterHeightMetres / .3048, 0)} ft clutter`,
      `Endpoint heights: ${formatNumber(profile.customerHeightMetres / .3048, 0)} ft customer · ${formatNumber(profile.towerHeightMetres / .3048, 0)} ft tower`,
      `${profile.samples.length} Google samples · ${formatNumber(profile.sampleSpacingMetres, 1)} m spacing · interpolation resolution ${resolution}`,
      "Approximate sampled planning model only. Unmapped trees/buildings, diffraction, atmospheric variation, and link budget are not evaluated. This is not an installation or service guarantee.",
    ];
  }

  function renderTerrainChart(profile) {
    if (!globalThis.Chart) {
      showProfileStatus("The packaged chart renderer is unavailable. No visual assessment is shown.", "error");
      return;
    }
    state.chart?.destroy();
    const points = selector => profile.samples.map(sample => ({ x: sample.distanceMetres / 1000, y: selector(sample) }));
    const dataset = (label, selector, extra) => ({ label, data: points(selector), pointRadius: 0, borderWidth: 2, ...extra });
    const pathDistanceKilometres = profile.distanceMetres / 1000;
    state.chart = new Chart(elements.terrain_chart, {
      type: "line",
      data: {
        datasets: [
          dataset("Terrain with curvature", sample => sample.terrainMetres, { borderColor: "#5f5a50", backgroundColor: "rgba(95,90,80,.22)", fill: "origin", borderWidth: 1 }),
          dataset("Clutter assumption", sample => sample.clutterTopMetres, { borderColor: "#3c7a62", backgroundColor: "rgba(60,122,98,.14)", fill: "-1", borderWidth: 1 }),
          dataset("Full first Fresnel zone", sample => sample.lineOfSightMetres + sample.firstFresnelRadiusMetres, { borderColor: "#4e86a7", backgroundColor: "rgba(78,134,167,.12)", borderDash: [5, 4], borderWidth: 1 }),
          dataset("Full first Fresnel zone lower", sample => sample.lineOfSightMetres - sample.firstFresnelRadiusMetres, { borderColor: "#4e86a7", backgroundColor: "rgba(78,134,167,.12)", borderDash: [5, 4], fill: "-1", borderWidth: 1 }),
          dataset("Selected Fresnel reference", sample => sample.lineOfSightMetres + sample.fresnelReferenceRadiusMetres, { borderColor: "#d66f35", borderDash: [7, 4], borderWidth: 1.5 }),
          dataset("Selected Fresnel reference lower", sample => sample.lineOfSightMetres - sample.fresnelReferenceRadiusMetres, { borderColor: "#d66f35", borderDash: [7, 4], borderWidth: 1.5 }),
          dataset("Line of sight", sample => sample.lineOfSightMetres, { borderColor: "#2f6f73", borderWidth: 3 }),
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: false,
        interaction: { intersect: false, mode: "index" },
        plugins: {
          legend: { labels: { boxWidth: 18, font: { size: 10 }, filter: item => [0, 1, 2, 4, 6].includes(item.datasetIndex) } },
          tooltip: { callbacks: { title: items => `${formatNumber(items[0].parsed.x, 3)} km from customer`, label: item => `${item.dataset.label}: ${formatNumber(item.parsed.y, 2)} m` } },
        },
        scales: {
          x: { type: "linear", min: 0, max: pathDistanceKilometres, bounds: "data", offset: false, title: { display: true, text: "Path distance (km)" }, ticks: { maxTicksLimit: 8, includeBounds: true }, grid: { display: false } },
          y: { title: { display: true, text: "Elevation (m)" } },
        },
      },
    });
  }

  function renderClearancePath(profile) {
    if (!state.map || !state.selectedCandidate) return;
    drawCandidatePaths(state.lastCandidates, state.customerLocation, state.selectedCandidate.rowNumber);
    const samples = profile.samples;
    let runStart = 0;
    let runColour = samples[1]?.clearanceCategory ?? "green";
    for (let index = 2; index < samples.length; index += 1) {
      const colour = samples[index].clearanceCategory ?? "green";
      if (colour === runColour) continue;
      addClearanceSegment(samples.slice(runStart, index), runColour);
      runStart = index - 1;
      runColour = colour;
    }
    addClearanceSegment(samples.slice(runStart), runColour);
  }

  function addClearanceSegment(samples, category) {
    if (samples.length < 2) return;
    const colours = { black: "#111111", red: "#d62828", orange: "#e8842f", yellow: "#d9b51c", green: "#2e8b57" };
    state.candidatePaths.push(new google.maps.Polyline({
      map: state.map,
      path: samples.map(sample => ({ lat: sample.latitude, lng: sample.longitude })),
      geodesic: true,
      clickable: false,
      strokeColor: colours[category] ?? colours.black,
      strokeOpacity: 1,
      strokeWeight: 5,
      zIndex: 90,
    }));
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
  elements.profile_close.addEventListener("click", closeProfile);
  elements.profile_refresh.addEventListener("click", () => {
    if (!state.selectedCandidate) return;
    state.plotRevision += 1;
    state.terrainSamples = null;
    requestElevation();
  });
  [elements.customer_height, elements.tower_height, elements.clutter_height, elements.frequency, elements.fresnel_reference, elements.earth_model]
    .forEach(control => control.addEventListener("change", () => {
      if (!state.terrainSamples || !state.selectedCandidate) return;
      state.plotRevision += 1;
      analyzeCachedTerrain();
    }));
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
