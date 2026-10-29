// monaco-diff.js - JavaScript para la vista MonacoDiff standalone

// Variables globales (se inicializan desde la vista)
let editorData = {};
let isNewFile = false;
let isDeletedFile = false;
let currentViewMode = 'split';
let monacoIframe = null;
let showLineNumbers = true;
let isMonacoReady = false;

// Función para inicializar con datos de la vista
function initializeEditorData(data) {
    editorData = data;
    isNewFile = !editorData.originalContent || editorData.originalContent.trim() === '';
    isDeletedFile = !editorData.modifiedContent || editorData.modifiedContent.trim() === '';
    currentViewMode = isNewFile || isDeletedFile ? 'inline' : 'split';
}

// Función para inicializar el iframe con archivo estático
function initializeMonacoIframe() {
    const iframe = document.getElementById('monacoIframe');
    const loadingOverlay = document.getElementById('loadingOverlay');

    if (!iframe || !loadingOverlay) {
        console.error('Elementos Monaco no encontrados');
        return;
    }

    const params = new URLSearchParams({
        original: encodeURIComponent(editorData.originalContent),
        modified: encodeURIComponent(editorData.modifiedContent),
        language: editorData.language,
        fileName: editorData.fileName
    });

    iframe.src = '/monaco-diff.html?' + params.toString();
    iframe.style.display = 'block';
    loadingOverlay.style.display = 'none';
    monacoIframe = iframe;
}

// Funciones de control
function changeViewMode(mode) {
    // Prevenir cambio a split si es archivo nuevo o eliminado
    if (mode === 'split' && (isNewFile || isDeletedFile)) {
        console.warn('Vista split no disponible para archivos nuevos o eliminados');
        return;
    }

    currentViewMode = mode;
    sendMessage({ type: 'changeViewMode', mode: mode });
    
    // Actualizar botones
    if (event && event.target) {
        updateButtonState(event.target, '.btn-group .btn');
    }

    // Actualizar visibilidad de headers
    updateHeadersVisibility();
}

function toggleLineNumbers() {
    showLineNumbers = !showLineNumbers;
    sendMessage({ type: 'toggleLineNumbers', show: showLineNumbers });
    
    if (event && event.target) {
        event.target.classList.toggle('active');
    }
}

function updateHeadersVisibility() {
    const headers = document.getElementById('diffHeaders');
    if (!headers) return;

    // Siempre mostrar headers en modo split
    if (currentViewMode === 'split') {
        headers.style.display = 'flex';
    } else {
        headers.style.display = 'none';
    }
}

function updateUIForFileType() {
    const splitBtn = document.getElementById('splitBtn');
    const inlineBtn = document.getElementById('inlineBtn');
    const prevBtn = document.getElementById('prevDiffBtn');
    const nextBtn = document.getElementById('nextDiffBtn');

    if (!splitBtn || !inlineBtn) return;

    if (isNewFile || isDeletedFile) {
        // Deshabilitar botón split
        splitBtn.disabled = true;
        splitBtn.classList.add('disabled');
        splitBtn.title = isNewFile ? 'No disponible para archivos nuevos' : 'No disponible para archivos eliminados';

        // Activar botón inline
        inlineBtn.classList.add('active');
        splitBtn.classList.remove('active');

        // Los botones de navegación siguen funcionando
        if (prevBtn) prevBtn.disabled = false;
        if (nextBtn) nextBtn.disabled = false;
    } else {
        // Activar botón split por defecto
        splitBtn.disabled = false;
        splitBtn.classList.remove('disabled');
        splitBtn.classList.add('active');
        splitBtn.title = 'Vista lado a lado';
        inlineBtn.classList.remove('active');
    }
}

function goToNextDiff() {
    sendMessage({ type: 'goToNextDiff' });
}

function goToPrevDiff() {
    sendMessage({ type: 'goToPrevDiff' });
}

function sendMessage(message) {
    if (monacoIframe && monacoIframe.contentWindow) {
        monacoIframe.contentWindow.postMessage(message, '*');
    }
}

function updateButtonState(activeButton, selector) {
    document.querySelectorAll(selector).forEach(btn => btn.classList.remove('active'));
    activeButton.classList.add('active');
}

// Listener para mensajes del iframe
function setupMessageListener() {
    window.addEventListener('message', function (event) {
        // Solo procesar si no estamos en un iframe nosotros mismos
        if (window !== window.top) return;

        switch (event.data.type) {
            case 'monacoReady':
                isMonacoReady = true;
                console.log('Monaco Editor está listo');

                // Si Monaco reporta un viewMode inicial diferente, sincronizar UI
                if (event.data.initialViewMode && event.data.initialViewMode !== currentViewMode) {
                    currentViewMode = event.data.initialViewMode;

                    // Actualizar botones
                    const splitBtn = document.getElementById('splitBtn');
                    const inlineBtn = document.getElementById('inlineBtn');

                    if (splitBtn && inlineBtn) {
                        if (currentViewMode === 'inline') {
                            splitBtn.classList.remove('active');
                            inlineBtn.classList.add('active');
                        } else {
                            splitBtn.classList.add('active');
                            inlineBtn.classList.remove('active');
                        }
                    }

                    // Actualizar headers
                    updateHeadersVisibility();
                }
                break;
            case 'requestData':
                sendMessage({ type: 'editorData', ...editorData });
                break;
        }
    });
}

// Shortcuts de teclado globales
function setupKeyboardShortcuts() {
    document.addEventListener('keydown', function (e) {
        if (e.ctrlKey || e.metaKey) {
            switch (e.key) {
                case 'ArrowDown':
                    e.preventDefault();
                    goToNextDiff();
                    break;
                case 'ArrowUp':
                    e.preventDefault();
                    goToPrevDiff();
                    break;
            }
        }
    });
}

// Función principal de inicialización
function initializeMonacoDiff(data) {
    initializeEditorData(data);
    setupMessageListener();
    setupKeyboardShortcuts();
    
    // Configurar UI según tipo de archivo
    updateUIForFileType();
    
    // Inicializar Monaco con delay
    setTimeout(initializeMonacoIframe, 200);
    
    // Configurar visibilidad inicial de headers
    updateHeadersVisibility();
}

// Auto-inicialización cuando el DOM esté listo
document.addEventListener('DOMContentLoaded', function () {
    // Verificar si los datos están disponibles globalmente
    if (typeof window.editorData !== 'undefined') {
        initializeMonacoDiff(window.editorData);
    }
});