(() => {
    const root = document.getElementById("station-details-page");
    if (!root) return;

    const estimateUrl = root.dataset.estimateUrl;
    const stationId = root.dataset.stationId;
    const maxPower = Number(root.dataset.maxPower || "0");
    const labelCurrent = root.dataset.labelCurrent || "Current";
    const labelDesired = root.dataset.labelDesired || "Desired";
    const labelEnergy = root.dataset.labelEnergy || "Energy";
    const labelDuration = root.dataset.labelDuration || "Duration";
    const labelCost = root.dataset.labelCost || "Cost";
    const labelMinute = root.dataset.labelMinute || "min";
    const labelCurrency = root.dataset.labelCurrency || "EUR";
    const canReserve = root.dataset.canReserve === "true";

    const vehicleButtons = Array.from(document.querySelectorAll(".vehicle-option"));
    const connectorButtons = Array.from(document.querySelectorAll(".connector-option"));
    const currentBattery = document.getElementById("current-battery");
    const desiredBattery = document.getElementById("desired-battery");
    const batteryLabel = document.getElementById("battery-label");

    const calcEnergy = document.getElementById("calc-energy");
    const calcSummary = document.getElementById("calc-summary");

    const reservationStart = document.getElementById("reservation-start");
    const reservationEnd = document.getElementById("reservation-end");
    const reservationEnergy = document.getElementById("reservation-energy");
    const reservationCost = document.getElementById("reservation-cost");
    const reserveButton = document.getElementById("reserve-button");
    const reservationForm = document.getElementById("reservation-form");

    const toLocalInput = (date) => {
        const pad = (v) => String(v).padStart(2, "0");
        return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
    };

    const getSelectedVehicleBattery = () => {
        const active = vehicleButtons.find((x) => x.classList.contains("active"));
        if (active) {
            return Number(active.dataset.battery || "0");
        }

        // If user has no vehicles, keep calculator and reserve flow functional using a default pack.
        return 60;
    };

    const getSelectedConnectorPower = () => {
        const active = connectorButtons.find((x) => x.classList.contains("active"));
        if (!active) return Math.max(11, Math.min(maxPower, 200));

        const name = (active.dataset.connectorName || "").toLowerCase();
        if (name.includes("ac") || name.includes("type 2")) {
            return 11;
        }

        return Math.max(22, Math.min(maxPower, 200));
    };

    const setActive = (buttons, target) => {
        buttons.forEach((button) => button.classList.remove("active"));
        target.classList.add("active");
    };

    // Ensure only one connector is active on initial render even if server markup marks multiple.
    if (connectorButtons.length > 0) {
        setActive(connectorButtons, connectorButtons[0]);
    }

    const updateReserveButton = (valid) => {
        if (!reserveButton) return;
        const enabled = canReserve && valid;
        reserveButton.disabled = !enabled;
        reserveButton.classList.toggle("is-disabled", !enabled);
    };

    if (!canReserve) {
        reservationForm?.addEventListener("submit", (event) => {
            event.preventDefault();
        });
    }

    const recalc = async () => {
        const currentPct = Number(currentBattery?.value || "25");
        const desiredPct = Number(desiredBattery?.value || "80");
        const batteryCapacity = getSelectedVehicleBattery();
        const connectorPower = getSelectedConnectorPower();

        const deltaPct = Math.max(0, desiredPct - currentPct);
        const energyKwh = Math.round((batteryCapacity * (deltaPct / 100)) * 100) / 100;
        const durationMinutes = Math.max(5, Math.ceil(((energyKwh || 1) / Math.max(connectorPower, 1)) * 60));

        if (batteryLabel) {
            batteryLabel.textContent = `${labelCurrent}: ${currentPct}% | ${labelDesired}: ${desiredPct}%`;
        }

        if (calcEnergy) {
            calcEnergy.textContent = `${labelEnergy}: ${energyKwh.toFixed(2)} kWh`;
        }

        if (reservationEnergy) {
            reservationEnergy.value = energyKwh.toFixed(2);
        }

        const start = reservationStart?.value ? new Date(reservationStart.value) : new Date(Date.now() + (30 * 60000));
        const end = new Date(start.getTime() + durationMinutes * 60000);

        if (reservationStart && !reservationStart.value) {
            reservationStart.value = toLocalInput(start);
        }

        if (reservationEnd) {
            reservationEnd.value = toLocalInput(end);
        }

        try {
            const url = `${estimateUrl}?stationId=${encodeURIComponent(stationId)}&durationMinutes=${encodeURIComponent(durationMinutes)}&estimatedKwh=${encodeURIComponent(energyKwh.toFixed(2))}`;
            const response = await fetch(url, { headers: { "X-Requested-With": "XMLHttpRequest" } });
            if (!response.ok) {
                throw new Error("Estimate failed");
            }

            const data = await response.json();
            const cost = Number(data.estimatedCost || 0);

            if (reservationCost) {
                reservationCost.value = cost.toFixed(2);
            }

            if (calcSummary) {
                calcSummary.textContent = `${labelDuration}: ${durationMinutes} ${labelMinute} | ${labelCost}: ${labelCurrency} ${cost.toFixed(2)}`;
            }

            updateReserveButton(desiredPct > currentPct);
        } catch {
            if (calcSummary) {
                calcSummary.textContent = `${labelDuration}: ${durationMinutes} ${labelMinute} | ${labelCost}: ${labelCurrency} 0.00`;
            }
            updateReserveButton(desiredPct > currentPct);
        }
    };

    vehicleButtons.forEach((button) => {
        button.addEventListener("click", () => {
            setActive(vehicleButtons, button);
            recalc();
        });
    });

    connectorButtons.forEach((button) => {
        button.addEventListener("click", () => {
            setActive(connectorButtons, button);
            recalc();
        });
    });

    currentBattery?.addEventListener("input", recalc);
    desiredBattery?.addEventListener("input", recalc);
    reservationStart?.addEventListener("change", recalc);

    recalc();
})();