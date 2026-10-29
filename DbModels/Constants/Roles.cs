namespace PasadorScript.DbModels.Constants
{
    public enum Rol
    {
        Analista = 1,
        DBA = 2,
        QA = 3,
        Supervisor = 4,
        IYC = 5
    }

    public class Roles
    {
        /*idRol	descripcion
            1	Analista
            2	DBA
            3	QA
            4	Supervisor
            5	IYC*/
        public static int Analista { get; } = 1;
        public static int DBA { get; } = 2;
        public static int QA { get; } = 3;
        public static int Supervisor { get; } = 4;
        public static int IYC { get; } = 5;

    }
}
