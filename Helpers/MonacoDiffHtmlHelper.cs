using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using System.Text.Encodings.Web;
using System.Web;

namespace PasadorScript.Helpers
{
    public static class MonacoDiffHtmlHelper
    {
        /// <summary>
        /// Genera un botón que abre MonacoDiff en una nueva pestaña
        /// </summary>
        public static IHtmlContent DiffButton(this IHtmlHelper html,
            int prId,
            string filePath,
            string buttonText = "Ver Diff",
            string buttonClass = "btn btn-sm btn-primary",
            string? returnUrl = null,
            bool openInNewTab = true)
        {
            var urlHelperFactory = html.ViewContext.HttpContext.RequestServices.GetService<IUrlHelperFactory>();
            var urlHelper = urlHelperFactory?.GetUrlHelper(html.ViewContext);
            var url = urlHelper?.Action("MonacoDiff", "PullRequest", new { prId = prId, filePath = filePath });

            if (!string.IsNullOrEmpty(returnUrl))
            {
                url += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
            }

            var button = new TagBuilder("a");
            button.Attributes["href"] = url;
            button.Attributes["class"] = buttonClass;

            if (openInNewTab)
            {
                button.Attributes["target"] = "_blank";
                button.Attributes["rel"] = "noopener noreferrer";
            }

            button.InnerHtml.SetHtmlContent($"<i class=\"bi bi-file-diff\"></i> {buttonText}");

            return button;
        }

        /// <summary>
        /// Genera un botón que abre MonacoDiff en un modal iframe
        /// </summary>
        public static IHtmlContent DiffModalButton(this IHtmlHelper html,
            int prId,
            string filePath,
            string contentOrig,
            string buttonText = "Ver Diff",
            string buttonClass = "btn btn-sm btn-primary")
        {
            // Codificar valores para JavaScript
            var encodedFilePath = System.Web.HttpUtility.JavaScriptStringEncode(filePath);
            var encodedContent = System.Web.HttpUtility.JavaScriptStringEncode(contentOrig);
            var fileName = System.IO.Path.GetFileName(filePath);

            // Crear el botón directamente con onclick
            var button = new TagBuilder("button");
            button.Attributes["type"] = "button";
            button.Attributes["class"] = buttonClass;
            button.Attributes["onclick"] =
                $"showDiffModal({prId}, '{encodedFilePath}', '{encodedContent.Replace("'", "\\'")}')";
            button.Attributes["title"] = "Ver diferencias";

            button.InnerHtml.AppendHtml($"<i class=\"bi bi-file-diff\"></i> {buttonText}");

            return button;
        }

        /// <summary>
        /// Incluye el modal de diff en la página
        /// </summary>
        public static IHtmlContent DiffModal(this IHtmlHelper html)
        {
            return html.Raw(@"
            <div class=""modal fade"" id=""diffModal"" tabindex=""-1"" aria-hidden=""true"">
                <div class=""modal-dialog modal-dialog-scrollable modal-xl"">
                    <div class=""modal-content"">
                        <div class=""modal-header"">
                            <h5 class=""modal-title"" id=""diffModalTitle"">Diferencias de Archivo</h5>
                            <button type=""button"" class=""btn-close"" data-bs-dismiss=""modal"" aria-label=""Close""></button>
                        </div>
                        <div class=""modal-body p-0"">
                            <!-- Formulario oculto para POST -->
                            <form id=""diffForm"" method=""post"" action=""/PullRequest/MonacoDiff"" target=""diffModalFrame"" style=""display: none;"">
                                <input type=""hidden"" name=""prId"" id=""diffPrId"" />
                                <input type=""hidden"" name=""filePath"" id=""diffFilePath"" />
                                <input type=""hidden"" name=""contentOrig"" id=""diffContentInput"" />
                                <input type=""hidden"" name=""inIframe"" value=""true"" />
                            </form>
                            
                            <div class=""flex justify-content-center align-items-center"" style=""height: 400px;"" id=""diffModalLoading"">
                                <div class=""spinner-border text-primary"" role=""status"">
                                    <span class=""visually-hidden"">Cargando...</span>
                                </div>
                            </div>
                            <iframe id=""diffModalFrame"" name=""diffModalFrame"" class=""w-100 h-100 border-0"" style=""display: none; min-height: 80vh;""></iframe>
                        </div>
                    </div>
                </div>
            </div>
            
            <script>
                function showDiffModal(prId, filePath, contentOrig) {
                    console.log('Opening diff modal:', { prId, filePath, contentLength: contentOrig.length });
                    
                    // Obtener elementos del DOM
                    const modal = document.getElementById('diffModal');
                    const frame = document.getElementById('diffModalFrame');
                    const loading = document.getElementById('diffModalLoading');
                    const titleElement = document.getElementById('diffModalTitle');
                    const form = document.getElementById('diffForm');
                    const prIdInput = document.getElementById('diffPrId');
                    const filePathInput = document.getElementById('diffFilePath');
                    const contentInput = document.getElementById('diffContentInput');
                    
                    // Validar que los elementos existan
                    if (!form || !prIdInput || !filePathInput || !contentInput) {
                        console.error('Form elements not found!');
                        alert('Error: Elementos del formulario no encontrados.');
                        return;
                    }
                    
                    // Extraer nombre de archivo para el título
                    const fileName = filePath.split('/').pop() || filePath.split('\\').pop() || 'Archivo';
                    titleElement.textContent = 'Diferencias: ' + fileName;
                    
                    // Mostrar loading
                    loading.style.display = 'flex';
                    frame.style.display = 'none';
                    
                    // Configurar valores del formulario
                    prIdInput.value = prId;
                    filePathInput.value = filePath;
                    contentInput.value = contentOrig;
                    
                    // Limpiar iframe anterior
                    frame.src = 'about:blank';
                    
                    // Enviar formulario
                    try {
                        form.submit();
                    } catch (error) {
                        console.error('Error submitting form:', error);
                        alert('Error al enviar el formulario: ' + error.message);
                        return;
                    }
                    
                    // Mostrar iframe cuando cargue
                    frame.onload = function() {
                        console.log('Diff iframe loaded');
                        loading.style.display = 'none';
                        frame.style.display = 'block';
                    };
                    
                    // Mostrar modal usando Bootstrap 5
                    const modalInstance = new bootstrap.Modal(modal);
                    modalInstance.show();
                }
                
                // Limpiar cuando se cierre el modal
                document.addEventListener('DOMContentLoaded', function() {
                    const modal = document.getElementById('diffModal');
                    if (modal) {
                        modal.addEventListener('hidden.bs.modal', function() {
                            const frame = document.getElementById('diffModalFrame');
                            if (frame) {
                                frame.src = 'about:blank';
                            }
                        });
                    }
                });
            </script>
        ");
        }
    }
}
