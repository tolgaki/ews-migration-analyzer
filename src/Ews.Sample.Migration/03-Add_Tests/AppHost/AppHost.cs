var builder = DistributedApplication.CreateBuilder(args);

var web = builder.AddProject<Projects.Contoso_Mail_Web>("mail-web");

builder.Build().Run();
