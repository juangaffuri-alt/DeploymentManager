(function () {
    const root = document.getElementById('informeGerencialRoot');
    if (!root) return;

    const form = document.getElementById('filterForm');
    const offcanvasEl = document.getElementById('filtrosOffcanvas');
    const hotfixField = document.getElementById('graficoHotfixField');
    const destinoField = document.getElementById('graficoDestinoField');
    const pruebaAcotadaField = document.getElementById('graficoPruebaAcotadaField');
    const agruparHotfixField = document.getElementById('agruparHotfixField');
    const agruparDestinoField = document.getElementById('agruparDestinoField');
    const agruparPruebaAcotadaField = document.getElementById('agruparPruebaAcotadaField');
    const chkHotfix = document.getElementById('chkGraficoHotfix');
    const chkDestino = document.getElementById('chkGraficoDestino');
    const chkPruebaAcotada = document.getElementById('chkGraficoPruebaAcotada');
    const chkAgruparHotfix = document.getElementById('chkAgruparHotfix');
    const chkAgruparDestino = document.getElementById('chkAgruparDestino');
    const chkAgruparPruebaAcotada = document.getElementById('chkAgruparPruebaAcotada');
    const badge = document.getElementById('filtrosBadge');
    const btnLimpiar = document.getElementById('btnLimpiar');
    const chipHotfix = document.getElementById('chipGraficoHotfix');
    const chipDestino = document.getElementById('chipGraficoDestino');
    const chipPruebaAcotada = document.getElementById('chipGraficoPruebaAcotada');
    const bloqueHotfix = document.getElementById('bloqueGraficoHotfix');
    const bloqueDestino = document.getElementById('bloqueGraficoDestino');
    const bloquePruebaAcotada = document.getElementById('bloqueGraficoPruebaAcotada');
    const rowGraficos = document.getElementById('graficosRow');
    const emptyHotfix = document.getElementById('kpiHotfixVacio');
    const emptyDestino = document.getElementById('kpiDestinoVacio');
    const emptyPruebaAcotada = document.getElementById('kpiPruebaAcotadaVacio');
    const canvasHotfix = document.getElementById('chartHotfix');
    const canvasDestino = document.getElementById('chartDestino');
    const canvasPruebaAcotada = document.getElementById('chartPruebaAcotada');
    const totalHotfixEl = document.getElementById('kpiHotfixTotal');
    const totalDestinoEl = document.getElementById('kpiDestinoTotal');
    const totalPruebaAcotadaEl = document.getElementById('kpiPruebaAcotadaTotal');
    const detHotfixEl = document.getElementById('kpiHotfixDetalle');
    const detDestinoEl = document.getElementById('kpiDestinoDetalle');
    const detPruebaAcotadaEl = document.getElementById('kpiPruebaAcotadaDetalle');
    const aclaracionHotfix = document.getElementById('kpiHotfixAclaracion');
    const aclaracionDestino = document.getElementById('kpiDestinoAclaracion');
    const aclaracionPruebaAcotada = document.getElementById('kpiPruebaAcotadaAclaracion');

    const kpiHotfix = readJson('kpi-hotfix-json');
    const kpiHotfixRef = readJson('kpi-hotfix-ref-json');
    const kpiDestino = readJson('kpi-destino-json');
    const kpiDestinoRef = readJson('kpi-destino-ref-json');
    const kpiPruebaAcotada = readJson('kpi-prueba-acotada-json');
    const kpiPruebaAcotadaRef = readJson('kpi-prueba-acotada-ref-json');
    const fechasSonDefault = root.getAttribute('data-fechas-default') === 'true';

    const COLOR = {
        hotfix: '#F25961',
        noHotfix: '#8d9498',
        noSalieron: '#6c757d',
        qa: '#FFAD46',
        prod: '#31CE36',
        pruebaSi: '#FFAD46',
        pruebaNo: '#31CE36',
        pruebaSinDatos: '#8d9498'
    };

    let chartHotfix = null;
    let chartDestino = null;
    let chartPruebaAcotada = null;
    let filterSnapshot = null;
    let skipRestore = false;

    function readJson(id) {
        const el = document.getElementById(id);
        if (!el) return null;
        try {
            return JSON.parse(el.textContent || '{}');
        } catch (e) {
            return null;
        }
    }

    function isOn(field) {
        return field && (field.value === 'true' || field.value === '1');
    }

    function integerPercents(counts) {
        const total = counts.reduce(function (a, b) { return a + b; }, 0);
        if (total === 0) return counts.map(function () { return 0; });
        const rounded = counts.map(function (c) { return Math.round((c / total) * 100); });
        let lastIdx = rounded.length - 1;
        for (let i = rounded.length - 1; i >= 0; i--) {
            if (counts[i] > 0) {
                lastIdx = i;
                break;
            }
        }
        const sum = rounded.reduce(function (a, b) { return a + b; }, 0);
        rounded[lastIdx] += (100 - sum);
        return rounded;
    }

    function setFlag(field, checkbox, on) {
        if (field) field.value = on ? 'true' : '';
        if (checkbox) checkbox.checked = !!on;
    }

    function syncFlagParam(url, name, on) {
        if (on) url.searchParams.set(name, 'true');
        else url.searchParams.delete(name);
    }

    function applyUrlFlags(url, state) {
        syncFlagParam(url, 'graficoHotfix', state.hotfixOn);
        syncFlagParam(url, 'graficoDestino', state.destinoOn);
        syncFlagParam(url, 'graficoPruebaAcotada', state.pruebaAcotadaOn);
        syncFlagParam(url, 'agruparHotfix', state.agruparHotfixOn);
        syncFlagParam(url, 'agruparDestino', state.agruparDestinoOn);
        syncFlagParam(url, 'agruparPruebaAcotada', state.agruparPruebaAcotadaOn);
    }

    function updateUrl(state) {
        const url = new URL(window.location.href);
        applyUrlFlags(url, state);
        window.history.replaceState({}, '', url.pathname + url.search);
    }

    function updatePagerLinks(state) {
        document.querySelectorAll('.pagination a.page-link').forEach(function (a) {
            if (!a.getAttribute('href')) return;
            const url = new URL(a.href, window.location.origin);
            applyUrlFlags(url, state);
            a.href = url.pathname + url.search;
        });
    }

    function countExtraChips() {
        return document.querySelectorAll('.informe-chip[data-chip="filtro"], .informe-chip[data-chip="grafico"]:not([hidden])').length;
    }

    function updateChrome(state) {
        if (chipHotfix) chipHotfix.hidden = !state.hotfixOn;
        if (chipDestino) chipDestino.hidden = !state.destinoOn;
        if (chipPruebaAcotada) chipPruebaAcotada.hidden = !state.pruebaAcotadaOn;

        const extra = countExtraChips();
        if (badge) {
            if (extra > 0) {
                badge.hidden = false;
                badge.textContent = String(extra);
            } else {
                badge.hidden = true;
            }
        }
        if (btnLimpiar) btnLimpiar.hidden = fechasSonDefault && extra === 0;
    }

    function destroyChart(chart) {
        if (chart) {
            chart.destroy();
        }
        return null;
    }

    function fillDetalle(el, rows) {
        if (!el) return;
        el.innerHTML = '';
        rows.forEach(function (row) {
            const li = document.createElement('li');
            li.textContent = row.label + ': ' + row.count + ' (' + row.pct + '%)';
            el.appendChild(li);
        });
    }

    function ensureDoughnut(canvas, existing, labels, data, colors, percents) {
        if (!canvas || typeof Chart === 'undefined') return existing;
        if (existing) return existing;

        const ctx = canvas.getContext('2d');
        const chart = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: labels,
                datasets: [{
                    data: data,
                    backgroundColor: colors,
                    borderWidth: 0
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutoutPercentage: 58,
                animation: { duration: 250 },
                legend: {
                    position: window.innerWidth >= 768 ? 'right' : 'bottom',
                    onClick: function () { },
                    labels: { boxWidth: 12, fontSize: 11 }
                },
                tooltips: {
                    callbacks: {
                        label: function (tooltipItem, chartData) {
                            const i = tooltipItem.index;
                            const label = chartData.labels[i];
                            const value = chartData.datasets[0].data[i];
                            return label + ': ' + value + ' (' + percents[i] + '%)';
                        }
                    }
                },
                hover: {
                    onHover: function (e) {
                        if (e && e.target) e.target.style.cursor = 'default';
                    }
                },
                onClick: function () { }
            }
        });
        requestAnimationFrame(function () {
            if (chart && typeof chart.resize === 'function') chart.resize();
        });
        return chart;
    }

    function renderHotfix(show, agrupado) {
        if (!bloqueHotfix) return;
        bloqueHotfix.hidden = !show;
        chartHotfix = destroyChart(chartHotfix);
        if (!show) return;

        const kpi = agrupado ? kpiHotfixRef : kpiHotfix;
        const total = kpi && typeof kpi.total === 'number' ? kpi.total : 0;
        if (totalHotfixEl) totalHotfixEl.textContent = (agrupado ? 'Referencias ' : 'Total ') + total;
        if (aclaracionHotfix) aclaracionHotfix.hidden = !(agrupado && total > 0);

        if (total === 0) {
            if (canvasHotfix) {
                canvasHotfix.hidden = true;
                if (canvasHotfix.parentElement) canvasHotfix.parentElement.hidden = true;
            }
            if (detHotfixEl) detHotfixEl.hidden = true;
            if (emptyHotfix) emptyHotfix.hidden = false;
            return;
        }

        if (emptyHotfix) emptyHotfix.hidden = true;
        if (canvasHotfix) {
            canvasHotfix.hidden = false;
            if (canvasHotfix.parentElement) canvasHotfix.parentElement.hidden = false;
        }
        if (detHotfixEl) detHotfixEl.hidden = false;

        const counts = [kpi.hotfix || 0, kpi.noHotfix || 0];
        const pcts = integerPercents(counts);
        fillDetalle(detHotfixEl, [
            { label: 'Hotfix', count: counts[0], pct: pcts[0] },
            { label: 'No hotfix', count: counts[1], pct: pcts[1] }
        ]);
        chartHotfix = ensureDoughnut(
            canvasHotfix,
            chartHotfix,
            ['Hotfix', 'No hotfix'],
            counts,
            [COLOR.hotfix, COLOR.noHotfix],
            pcts);
    }

    function renderDestino(show, agrupado) {
        if (!bloqueDestino) return;
        bloqueDestino.hidden = !show;
        chartDestino = destroyChart(chartDestino);
        if (!show) return;

        const kpi = agrupado ? kpiDestinoRef : kpiDestino;
        const total = kpi && typeof kpi.total === 'number' ? kpi.total : 0;
        if (totalDestinoEl) totalDestinoEl.textContent = (agrupado ? 'Referencias ' : 'Total ') + total;
        if (aclaracionDestino) aclaracionDestino.hidden = !(agrupado && total > 0);

        if (total === 0) {
            if (canvasDestino) {
                canvasDestino.hidden = true;
                if (canvasDestino.parentElement) canvasDestino.parentElement.hidden = true;
            }
            if (detDestinoEl) detDestinoEl.hidden = true;
            if (emptyDestino) emptyDestino.hidden = false;
            return;
        }

        if (emptyDestino) emptyDestino.hidden = true;
        if (canvasDestino) {
            canvasDestino.hidden = false;
            if (canvasDestino.parentElement) canvasDestino.parentElement.hidden = false;
        }
        if (detDestinoEl) detDestinoEl.hidden = false;

        const counts = [
            kpi.noSalieron || 0,
            kpi.enQa || 0,
            kpi.enProd || 0
        ];
        const pcts = integerPercents(counts);
        fillDetalle(detDestinoEl, [
            { label: 'No salieron', count: counts[0], pct: pcts[0] },
            { label: 'En QA', count: counts[1], pct: pcts[1] },
            { label: 'En PROD', count: counts[2], pct: pcts[2] }
        ]);
        chartDestino = ensureDoughnut(
            canvasDestino,
            chartDestino,
            ['No salieron', 'En QA', 'En PROD'],
            counts,
            [COLOR.noSalieron, COLOR.qa, COLOR.prod],
            pcts);
    }

    function renderPruebaAcotada(show, agrupado) {
        if (!bloquePruebaAcotada) return;
        bloquePruebaAcotada.hidden = !show;
        chartPruebaAcotada = destroyChart(chartPruebaAcotada);
        if (!show) return;

        const kpi = agrupado ? kpiPruebaAcotadaRef : kpiPruebaAcotada;
        const total = kpi && typeof kpi.total === 'number' ? kpi.total : 0;
        if (totalPruebaAcotadaEl) totalPruebaAcotadaEl.textContent = (agrupado ? 'Referencias ' : 'Total ') + total;
        if (aclaracionPruebaAcotada) aclaracionPruebaAcotada.hidden = !(agrupado && total > 0);

        if (total === 0) {
            if (canvasPruebaAcotada) {
                canvasPruebaAcotada.hidden = true;
                if (canvasPruebaAcotada.parentElement) canvasPruebaAcotada.parentElement.hidden = true;
            }
            if (detPruebaAcotadaEl) detPruebaAcotadaEl.hidden = true;
            if (emptyPruebaAcotada) emptyPruebaAcotada.hidden = false;
            return;
        }

        if (emptyPruebaAcotada) emptyPruebaAcotada.hidden = true;
        if (canvasPruebaAcotada) {
            canvasPruebaAcotada.hidden = false;
            if (canvasPruebaAcotada.parentElement) canvasPruebaAcotada.parentElement.hidden = false;
        }
        if (detPruebaAcotadaEl) detPruebaAcotadaEl.hidden = false;

        const counts = [kpi.si || 0, kpi.no || 0, kpi.sinDatos || 0];
        const pcts = integerPercents(counts);
        fillDetalle(detPruebaAcotadaEl, [
            { label: 'Sí', count: counts[0], pct: pcts[0] },
            { label: 'No', count: counts[1], pct: pcts[1] },
            { label: 'Sin datos', count: counts[2], pct: pcts[2] }
        ]);
        chartPruebaAcotada = ensureDoughnut(
            canvasPruebaAcotada,
            chartPruebaAcotada,
            ['Sí', 'No', 'Sin datos'],
            counts,
            [COLOR.pruebaSi, COLOR.pruebaNo, COLOR.pruebaSinDatos],
            pcts);
    }

    function currentGraphState() {
        return {
            hotfixOn: isOn(hotfixField),
            destinoOn: isOn(destinoField),
            pruebaAcotadaOn: isOn(pruebaAcotadaField),
            agruparHotfixOn: isOn(agruparHotfixField),
            agruparDestinoOn: isOn(agruparDestinoField),
            agruparPruebaAcotadaOn: isOn(agruparPruebaAcotadaField)
        };
    }

    function applyGraphState(state, persistUrl) {
        setFlag(hotfixField, chkHotfix, state.hotfixOn);
        setFlag(destinoField, chkDestino, state.destinoOn);
        setFlag(pruebaAcotadaField, chkPruebaAcotada, state.pruebaAcotadaOn);
        setFlag(agruparHotfixField, chkAgruparHotfix, state.agruparHotfixOn);
        setFlag(agruparDestinoField, chkAgruparDestino, state.agruparDestinoOn);
        setFlag(agruparPruebaAcotadaField, chkAgruparPruebaAcotada, state.agruparPruebaAcotadaOn);
        if (rowGraficos) rowGraficos.hidden = !state.hotfixOn && !state.destinoOn && !state.pruebaAcotadaOn;
        renderHotfix(state.hotfixOn, state.agruparHotfixOn);
        renderDestino(state.destinoOn, state.agruparDestinoOn);
        renderPruebaAcotada(state.pruebaAcotadaOn, state.agruparPruebaAcotadaOn);
        updateChrome(state);
        updatePagerLinks(state);
        if (persistUrl) updateUrl(state);
    }

    const FILTER_IDS = [
        'fechaDesde', 'fechaHasta', 'fechaImplementadoDesde', 'fechaImplementadoHasta',
        'repoKey', 'tipo', 'hotfix',
        'tipoReferencia', 'nroIncidente', 'usuario', 'ultimoStage'
    ];

    function snapshotFilters() {
        const snap = {};
        FILTER_IDS.forEach(function (id) {
            const el = document.getElementById(id);
            snap[id] = el ? el.value : '';
        });
        return snap;
    }

    function restoreFilters(snap) {
        if (!snap) return;
        FILTER_IDS.forEach(function (id) {
            const el = document.getElementById(id);
            if (el) el.value = snap[id];
        });
    }

    function disableEmptyFlags() {
        if (hotfixField && !isOn(hotfixField)) hotfixField.disabled = true;
        if (destinoField && !isOn(destinoField)) destinoField.disabled = true;
        if (pruebaAcotadaField && !isOn(pruebaAcotadaField)) pruebaAcotadaField.disabled = true;
        if (agruparHotfixField && !isOn(agruparHotfixField)) agruparHotfixField.disabled = true;
        if (agruparDestinoField && !isOn(agruparDestinoField)) agruparDestinoField.disabled = true;
        if (agruparPruebaAcotadaField && !isOn(agruparPruebaAcotadaField)) agruparPruebaAcotadaField.disabled = true;
    }

    function enableGraphFields() {
        if (hotfixField) hotfixField.disabled = false;
        if (destinoField) destinoField.disabled = false;
        if (pruebaAcotadaField) pruebaAcotadaField.disabled = false;
        if (agruparHotfixField) agruparHotfixField.disabled = false;
        if (agruparDestinoField) agruparDestinoField.disabled = false;
        if (agruparPruebaAcotadaField) agruparPruebaAcotadaField.disabled = false;
    }

    if (chkHotfix) {
        chkHotfix.addEventListener('change', function () {
            const s = currentGraphState();
            s.hotfixOn = chkHotfix.checked;
            applyGraphState(s, true);
        });
    }
    if (chkDestino) {
        chkDestino.addEventListener('change', function () {
            const s = currentGraphState();
            s.destinoOn = chkDestino.checked;
            applyGraphState(s, true);
        });
    }
    if (chkPruebaAcotada) {
        chkPruebaAcotada.addEventListener('change', function () {
            const s = currentGraphState();
            s.pruebaAcotadaOn = chkPruebaAcotada.checked;
            applyGraphState(s, true);
        });
    }
    if (chkAgruparHotfix) {
        chkAgruparHotfix.addEventListener('change', function () {
            const s = currentGraphState();
            s.agruparHotfixOn = chkAgruparHotfix.checked;
            applyGraphState(s, true);
        });
    }
    if (chkAgruparDestino) {
        chkAgruparDestino.addEventListener('change', function () {
            const s = currentGraphState();
            s.agruparDestinoOn = chkAgruparDestino.checked;
            applyGraphState(s, true);
        });
    }
    if (chkAgruparPruebaAcotada) {
        chkAgruparPruebaAcotada.addEventListener('change', function () {
            const s = currentGraphState();
            s.agruparPruebaAcotadaOn = chkAgruparPruebaAcotada.checked;
            applyGraphState(s, true);
        });
    }

    document.querySelectorAll('[data-dismiss-graph]').forEach(function (btn) {
        btn.addEventListener('click', function (e) {
            e.preventDefault();
            const which = btn.getAttribute('data-dismiss-graph');
            const s = currentGraphState();
            if (which === 'hotfix') s.hotfixOn = false;
            if (which === 'destino') s.destinoOn = false;
            if (which === 'pruebaAcotada') s.pruebaAcotadaOn = false;
            applyGraphState(s, true);
        });
    });

    if (form) {
        form.addEventListener('submit', function () {
            skipRestore = true;
            disableEmptyFlags();
        });
    }

    if (offcanvasEl) {
        offcanvasEl.addEventListener('show.bs.offcanvas', function () {
            filterSnapshot = snapshotFilters();
            skipRestore = false;
        });
        offcanvasEl.addEventListener('hide.bs.offcanvas', function () {
            if (!skipRestore) restoreFilters(filterSnapshot);
        });
    }

    const input = document.getElementById('usuario');
    const sugerencias = document.getElementById('sugerencias');
    const buscarUrl = root.getAttribute('data-buscar-usuario-url') || '/RuntimeHelper/BuscarUsuario';
    const tablasType = root.getAttribute('data-tablas-type') || '0';

    function positionSugerencias() {
        if (!input || !sugerencias) return;
        const rect = input.getBoundingClientRect();
        sugerencias.style.position = 'fixed';
        sugerencias.style.left = rect.left + 'px';
        sugerencias.style.top = (rect.bottom + 2) + 'px';
        sugerencias.style.width = rect.width + 'px';
        sugerencias.style.zIndex = '1100';
    }

    if (input && sugerencias) {
        if (sugerencias.parentElement !== document.body) {
            document.body.appendChild(sugerencias);
        }

        input.addEventListener('input', async function () {
            const query = this.value;
            if (query.length < 2) {
                sugerencias.classList.remove('show');
                sugerencias.innerHTML = '';
                return;
            }

            const response = await fetch(
                buscarUrl + '?term=' + encodeURIComponent(query) + '&tablasType=' + encodeURIComponent(tablasType));
            const data = await response.json();

            if (!data.length) {
                sugerencias.classList.remove('show');
                sugerencias.innerHTML = '';
                return;
            }

            sugerencias.innerHTML = '';
            data.forEach(function (item) {
                const a = document.createElement('a');
                a.classList.add('dropdown-item');
                a.href = '#';
                a.textContent = item;
                a.onclick = function (e) {
                    e.preventDefault();
                    input.value = item;
                    sugerencias.classList.remove('show');
                    sugerencias.innerHTML = '';
                };
                sugerencias.appendChild(a);
            });
            positionSugerencias();
            sugerencias.classList.add('show');
        });

        document.addEventListener('click', function (e) {
            if (!input.contains(e.target) && !sugerencias.contains(e.target)) {
                sugerencias.classList.remove('show');
            }
        });

        window.addEventListener('scroll', function () {
            if (sugerencias.classList.contains('show')) positionSugerencias();
        }, true);
        window.addEventListener('resize', function () {
            if (sugerencias.classList.contains('show')) positionSugerencias();
        });
    }

    const excelUrl = root.getAttribute('data-excel-url');
    document.getElementById('btnExportExcel')?.addEventListener('click', function (e) {
        e.preventDefault();
        if (this.disabled) return;
        if (!form || !excelUrl) return;
        disableEmptyFlags();
        const params = new URLSearchParams(new FormData(form));
        enableGraphFields();
        window.open(excelUrl + '?' + params.toString(), '_blank');
    });

    applyGraphState(currentGraphState(), false);
})();
