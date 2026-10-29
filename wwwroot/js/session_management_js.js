// SessionManager.js - Manejo de sesiones expiradas en el frontend

class SessionManager {
    constructor() {
        this.checkInterval = 1 * 60 * 1000; // Verificar cada 1 minutos
        this.warningTime = 5 * 60 * 1000; // Advertir 5 minutos antes
        this.sessionCheckTimer = null;
        this.warningShown = false;

        this.init();
    }

    init() {
        // Interceptar todas las peticiones AJAX para detectar 401
        this.setupAjaxInterceptors();

        // Iniciar verificación periódica de sesión
        this.startSessionCheck();

        // Detectar actividad del usuario para reiniciar timer
        //this.setupActivityDetection();
    }

    setupAjaxInterceptors() {
        // Para jQuery
        if (typeof $ !== 'undefined') {
            $(document).ajaxError((event, xhr, settings) => {
                if (xhr.status === 401) {
                    this.handleSessionExpired();
                }
            });
        }

        // Para fetch API - VERSIÓN MEJORADA
        const originalFetch = window.fetch;
        window.fetch = async (...args) => {
            try {
                const response = await originalFetch.call(window, ...args);

                // Solo manejar 401 para sesiones expiradas
                // NO interferir con otros códigos de error
                if (response.status === 401) {
                    this.handleSessionExpired();
                }

                return response;
            } catch (error) {
                // Si hay un error de red o similar, simplemente re-lanzarlo
                // No interferir con el manejo normal de errores
                throw error;
            }
        };
    }

    startSessionCheck() {
        this.sessionCheckTimer = setInterval(() => {
            this.checkSessionStatus();
        }, this.checkInterval);
    }

    async checkSessionStatus() {
        if (this.isChecking) return;
            this.isChecking = true;
        
        try {
            const response = await fetch('/Account/CheckSession');

            const data = await response.json();

            switch (data.status) {
                case "expired":
                    //$('#modalSessionExpired').modal('show');
                    this.showSessionExpiredModal();
                    break;
                case "valid":
                    console.log("Sesión válida");
                    break;
                case "no-auth":
                    console.log("Usuario no autenticado, ignorando.");
                    break;
            }
        } catch (e) {
            console.error("Error al verificar sesión:", e);
        } finally {
            this.isChecking = false;
        }
    }

    handleSessionExpired() {
        if (this.warningShown || window.location.pathname === "/Account/Signin") return;
    
        this.warningShown = true;
        clearInterval(this.sessionCheckTimer);
    
        // Deshabilitar futuras verificaciones
        this.isChecking = true;
    
        this.showSessionExpiredModal();
    }

    showSessionExpiredModal() {
        console.log("ExpiredModal");
        // Ejemplo con SweetAlert2 (si está disponible)
        if (typeof Swal !== 'undefined') {
            Swal.fire({
                title: 'Sesión Expirada',
                text: 'Su sesión ha expirado. Será redirigido a la página de inicio de sesión.',
                icon: 'warning',
                confirmButtonText: 'Iniciar Sesión',
                allowOutsideClick: false,
                allowEscapeKey: false
            }).then(() => {
                window.location.href = '/Account/Signin';
            });
        }
        // Ejemplo con modal de Bootstrap (si está disponible)
        else if (typeof bootstrap !== 'undefined') {
            const modalHtml = `
                <div class="modal fade" id="sessionExpiredModal" tabindex="-1" data-bs-backdrop="static">
                    <div class="modal-dialog">
                        <div class="modal-content">
                            <div class="modal-header">
                                <h5 class="modal-title">Sesión Expirada</h5>
                            </div>
                            <div class="modal-body">
                                Su sesión ha expirado. Será redirigido a la página de inicio de sesión.
                            </div>
                            <div class="modal-footer">
                                <button type="button" class="btn btn-primary" onclick="window.location.href='/Account/Signin'">
                                    Iniciar Sesión
                                </button>
                            </div>
                        </div>
                    </div>
                </div>`;

            document.body.insertAdjacentHTML('beforeend', modalHtml);
            const modal = new bootstrap.Modal(document.getElementById('sessionExpiredModal'));
            modal.show();
        }
        // Fallback con alert nativo
        else {
            alert('Su sesión ha expirado. Será redirigido a la página de inicio de sesión.');
            window.location.href = '/Account/Signin';
        }
    }

    setupActivityDetection() {
        const events = ['mousedown', 'mousemove', 'keypress', 'scroll', 'touchstart', 'click'];
        let activityTimer = null;

        const resetActivityTimer = () => {
            clearTimeout(activityTimer);
            activityTimer = setTimeout(() => {
                // Usuario inactivo por mucho tiempo, verificar sesión
                this.checkSessionStatus();
            }, 1 * 60 * 1000); // 30 minutos de inactividad
        };

        events.forEach(event => {
            document.addEventListener(event, resetActivityTimer, true);
        });
    }

    // Método para extender sesión manualmente
    async extendSession() {
        try {
            const response = await fetch('/Account/ExtendSession', {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            if (response.ok) {
                const data = await response.json();
                return data.success;
            }

            return false;
        } catch (error) {
            console.error('Error extending session:', error);
            return false;
        }
    }

    // Método para limpiar timers al cerrar la página
    cleanup() {
        if (this.sessionCheckTimer) {
            clearInterval(this.sessionCheckTimer);
        }
    }
}

// Inicializar el gestor de sesiones cuando se carga la página
document.addEventListener('DOMContentLoaded', () => {
    window.sessionManager = new SessionManager();
});

// Limpiar al cerrar la página
window.addEventListener('beforeunload', () => {
    if (window.sessionManager) {
        window.sessionManager.cleanup();
    }
});

// Función auxiliar para mostrar countdown de expiración (opcional)
function showSessionWarning(minutesLeft) {
    if (typeof Swal !== 'undefined') {
        Swal.fire({
            title: 'Sesión por Expirar',
            text: `Su sesión expirará en ${minutesLeft} minutos. ¿Desea extenderla?`,
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Extender Sesión',
            cancelButtonText: 'Cerrar Sesión'
        }).then((result) => {
            if (result.isConfirmed) {
                window.sessionManager.extendSession().then(success => {
                    if (!success) {
                        window.location.href = '/Account/Signin';
                    }
                });
            } else {
                window.location.href = '/Account/SignOut';
            }
        });
    }
}