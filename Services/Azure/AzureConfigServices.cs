   public class AzureGxConfig : IAzureServiceConfig
   {
       public string ProjectName => "GXProject";
       public string RepositoryId => "repo-gx-id";
       public int DefinitionId => 123;
   }

   public class AzureOtroConfig : IAzureServiceConfig
   {
       public string ProjectName => "OtroProject";
       public string RepositoryId => "repo-otro-id";
       public int DefinitionId => 456;
   }
   