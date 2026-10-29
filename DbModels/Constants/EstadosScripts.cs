namespace PasadorScript.DbModels.Constants
{
    /*  1	Liberado -> Esperando DBA
        2	Aprobado DBA -> Esperando QA
        3	Aprobado QA -> Esperando IYC 
        4	Aplicado en QA -> Esperando Supervisor
        5	Aprobado Supervisor -> Esperando IYC
        6	Aplicado en PROD
        7	Rechazado 
    */
    public class EstadosScripts
    {
        public static int Liberado { get; set; } = 1;
        public static int AprobadoDBA { get; set; } = 2;
        public static int AprobadoQA { get; set; } = 3;
        public static int AplicadoQA { get; set; } = 4;
        public static int AprobadoSup { get; set; } = 5;
        public static int AplicadoPROD { get; set; } = 6;
        public static int Rechazado { get; set; } = 7;

        public static int GetEstadoAprobacionSegunRol(int rol, int ultEstado = 0)
        {
            if (rol == Roles.DBA)
                return AprobadoDBA;
            else if (rol == Roles.QA) 
                return AprobadoQA;
            else if (rol == Roles.Supervisor)
                return AprobadoSup;
            else if (rol == Roles.IYC)
            {
                if (ultEstado != 0 && ultEstado == AprobadoDBA)
                    return AplicadoQA;
                else if (ultEstado != 0)
                    return AplicadoPROD;
                else
                    return 0;
            }
            return 0;
        }

    }


}
