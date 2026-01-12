using System;
using OpenAPI.Client;
using OpenAPI.Api;

var config = new Configuration
{
    BasePath = "http://localhost:5050"
};

var api = new DefaultApi(config);
var metrics = api.MetricsResilienceGet();
Console.WriteLine(metrics);
