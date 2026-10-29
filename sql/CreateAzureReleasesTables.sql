/* =============================================================================
   DeploymentManager - Volcado de Releases de Azure DevOps a BD (auditoría)
   Destino: base DeploymentManager_DF (misma connection string "NuevoP229")
   Idempotente: puede ejecutarse más de una vez sin generar error.
   ============================================================================= */

IF DB_NAME() <> 'DeploymentManager_DF'
    PRINT 'ADVERTENCIA: este script se está ejecutando en la base [' + DB_NAME() + ']. Verifique que sea DeploymentManager_DF.';
GO

/* -----------------------------------------------------------------------------
   1) Cabecera de release (una fila por release de Azure DevOps).
      Clave natural: (ProjectName, ReleaseId) -- los IDs se repiten entre proyectos.
------------------------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.AzureRelease', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AzureRelease
    (
        Id                  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AzureRelease PRIMARY KEY,
        ProjectName         NVARCHAR(255)  NOT NULL,              -- proyecto Azure DevOps
        ReleaseDefinitionId INT            NOT NULL,              -- definición/pipeline usada en la consulta
        ReleaseId           INT            NOT NULL,              -- id del release en Azure
        ReleaseName         NVARCHAR(500)  NULL,
        Status              NVARCHAR(50)   NULL,                  -- active / abandoned / inProgress...
        CreatedOnUtc        DATETIME       NOT NULL,              -- UTC tal como lo devuelve Azure
        ModifiedOnUtc       DATETIME       NULL,                  -- UTC
        PullRequestId       INT            NULL,                  -- extraído de artifacts (o vía build)
        BuildId             INT            NULL,                  -- build asociado del artifact
        RepositoryId        UNIQUEIDENTIFIER NULL,                -- repo Git del artifact
        BuildNumber         NVARCHAR(255)  NULL,
        ReleaseUrl          NVARCHAR(1000) NULL,                  -- _links.web.href
        Description         NVARCHAR(MAX)  NULL,
        RawJson             NVARCHAR(MAX)  NULL,                  -- payload COMPLETO del release (salvaguarda)
        FirstSyncAtUtc      DATETIME       NOT NULL CONSTRAINT DF_AzureRelease_FirstSync  DEFAULT (SYSUTCDATETIME()),
        LastSyncAtUtc       DATETIME       NOT NULL CONSTRAINT DF_AzureRelease_LastSync   DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_AzureRelease_Project_Release UNIQUE (ProjectName, ReleaseId)
    );

    CREATE INDEX IX_AzureRelease_CreatedOnUtc ON dbo.AzureRelease (CreatedOnUtc);
    CREATE INDEX IX_AzureRelease_PullRequest  ON dbo.AzureRelease (PullRequestId);
END
GO

/* -----------------------------------------------------------------------------
   2) Environments (stages) de cada release. Incluye los nunca desplegados.
------------------------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.AzureReleaseEnvironment', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AzureReleaseEnvironment
    (
        Id                    BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AzureReleaseEnvironment PRIMARY KEY,
        AzureReleaseId        BIGINT         NOT NULL CONSTRAINT FK_AzureReleaseEnv_Release
                                REFERENCES dbo.AzureRelease(Id) ON DELETE CASCADE,
        EnvironmentId         INT            NOT NULL,            -- id del environment en Azure (0 si no vino)
        Name                  NVARCHAR(255)  NOT NULL,            -- QA, DF, PROD, PRO (BT/BTS)...
        Rank                  INT            NOT NULL DEFAULT (0),-- orden dentro de la definición
        Status                NVARCHAR(50)   NULL,                -- succeeded / failed / notStarted...
        Attempt               INT            NULL,                -- último attempt de deploy
        QueuedOnUtc           DATETIME       NULL,                -- cola del último deploy real
        StartedOnUtc          DATETIME       NULL,
        CompletedOnUtc        DATETIME       NULL,                -- fin del último deploy step aplicado
        DeployedByDisplayName NVARCHAR(255)  NULL,
        RawJson               NVARCHAR(MAX)  NULL,                -- nodo JSON del environment (salvaguarda)
        FirstSyncAtUtc        DATETIME       NOT NULL DEFAULT (SYSUTCDATETIME()),
        LastSyncAtUtc         DATETIME       NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_AzureReleaseEnv UNIQUE (AzureReleaseId, Name, Rank)
    );

    CREATE INDEX IX_AzureReleaseEnv_Release ON dbo.AzureReleaseEnvironment (AzureReleaseId);
END
GO

/* -----------------------------------------------------------------------------
   3) Aprobaciones PRE/POST de cada environment. Histórico de attempts:
      se re-sincroniza pero NUNCA se borran filas (auditabilidad).
------------------------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.AzureReleaseApproval', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AzureReleaseApproval
    (
        Id                     BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AzureReleaseApproval PRIMARY KEY,
        AzureReleaseEnvId      BIGINT         NOT NULL CONSTRAINT FK_AzureReleaseApproval_Env
                                 REFERENCES dbo.AzureReleaseEnvironment(Id) ON DELETE CASCADE,
        ApprovalType           VARCHAR(4)     NOT NULL CONSTRAINT CK_AzureReleaseApproval_Type
                                 CHECK (ApprovalType IN ('PRE','POST')),
        ApprovalId             INT            NULL,                -- id de la aprobación en Azure
        Rank                   INT            NOT NULL DEFAULT (0),
        Attempt                INT            NOT NULL DEFAULT (0),
        Status                 NVARCHAR(50)   NULL,                -- approved / rejected / pending
        IsAutomated            BIT            NOT NULL DEFAULT (0),
        ApproverId             NVARCHAR(255)  NULL,                -- uniqueName/id del requerido
        ApproverDisplayName    NVARCHAR(255)  NULL,
        ApprovedByDisplayName  NVARCHAR(255)  NULL,                -- quién aprobó realmente
        Comment                NVARCHAR(1000) NULL,
        ModifiedOnUtc          DATETIME       NULL,                -- momento del cambio de estado
        RawJson                NVARCHAR(MAX)  NULL,
        FirstSyncAtUtc         DATETIME       NOT NULL DEFAULT (SYSUTCDATETIME()),
        LastSyncAtUtc          DATETIME       NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_AzureReleaseApproval UNIQUE (AzureReleaseEnvId, ApprovalType, Attempt, Rank)
    );

    CREATE INDEX IX_AzureReleaseApproval_Env ON dbo.AzureReleaseApproval (AzureReleaseEnvId);
END
GO

/* -----------------------------------------------------------------------------
   4) Bitácora de ejecuciones del sincronizador (traza de auditoría mínima).
------------------------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.AzureReleaseSyncLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AzureReleaseSyncLog
    (
        Id               BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AzureReleaseSyncLog PRIMARY KEY,
        StartedAtUtc     DATETIME      NOT NULL,
        FinishedAtUtc    DATETIME      NULL,
        MinCreatedUtc    DATETIME      NULL,                  -- ventana pedida a Azure
        MaxCreatedUtc    DATETIME      NULL,
        DefinitionsTotal INT           NOT NULL DEFAULT (0),
        DefinitionsOk    INT           NOT NULL DEFAULT (0),
        ReleasesUpserted INT           NOT NULL DEFAULT (0),
        Errors           INT           NOT NULL DEFAULT (0),
        Truncated        BIT           NOT NULL DEFAULT (0),  -- se alcanzó el tope de páginas
        Success          BIT           NOT NULL DEFAULT (0),
        Message          NVARCHAR(MAX) NULL
    );
END
GO
