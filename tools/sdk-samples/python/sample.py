from openapi_client import ApiClient
from openapi_client.api.default_api import DefaultApi

with ApiClient() as api_client:
    api = DefaultApi(api_client)
    metrics = api.metrics_resilience_get()
    print(metrics)
