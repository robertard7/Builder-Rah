use openapi::apis::configuration::Configuration;
use openapi::apis::default_api::metrics_resilience_get;

#[tokio::main]
async fn main() {
    let mut config = Configuration::new();
    config.base_path = "http://localhost:5050".to_string();
    let metrics = metrics_resilience_get(&config).await;
    println!("{:?}", metrics);
}
