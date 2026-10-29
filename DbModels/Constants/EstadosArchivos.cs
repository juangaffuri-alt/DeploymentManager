namespace PasadorScript.DbModels.Constants
{
    /*
        1	Aplicado en QA
        2	Aplicado en PROD
     */
    public static class EstadosArchivos
    {
        public static int None { get; set; } = 1;
        public static int AplicadoQA { get; set; } = 2;
        public static int AplicadoPROD { get; set; } = 3;
    }
}
