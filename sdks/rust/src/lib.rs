//! Nexus HyperIntelligence Rust SDK
//!
//! Uses only the standard library for HTTP (no tokio/reqwest dep) so it
//! compiles without external packages in offline / CI environments.
//! For production use, enable the `async` feature and add reqwest.

use std::io::{Read, Write};
use std::net::TcpStream;
use std::fmt;

use serde::{Deserialize, Serialize};
use serde_json::Value;

// ── Error ─────────────────────────────────────────────────────────────────────

#[derive(Debug)]
pub enum NexusError {
    Http { status: u16, body: String },
    Io(std::io::Error),
    Json(serde_json::Error),
}

impl fmt::Display for NexusError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            NexusError::Http { status, body } => write!(f, "HTTP {status}: {body}"),
            NexusError::Io(e) => write!(f, "IO: {e}"),
            NexusError::Json(e) => write!(f, "JSON: {e}"),
        }
    }
}
impl std::error::Error for NexusError {}
impl From<std::io::Error> for NexusError { fn from(e: std::io::Error) -> Self { Self::Io(e) } }
impl From<serde_json::Error> for NexusError { fn from(e: serde_json::Error) -> Self { Self::Json(e) } }

// ── Client ────────────────────────────────────────────────────────────────────

pub struct NexusClient {
    host: String,
    port: u16,
    tenant_id: String,
}

impl NexusClient {
    pub fn new(host: &str, port: u16, tenant_id: &str) -> Self {
        Self {
            host: host.to_string(),
            port,
            tenant_id: tenant_id.to_string(),
        }
    }

    fn request(&self, method: &str, path: &str, body: Option<&str>) -> Result<Value, NexusError> {
        let addr = format!("{}:{}", self.host, self.port);
        let mut stream = TcpStream::connect(&addr)?;

        let body_str = body.unwrap_or("");
        let content_len = body_str.len();
        let request = format!(
            "{method} {path} HTTP/1.0\r\n\
             Host: {}\r\n\
             Content-Type: application/json\r\n\
             Accept: application/json\r\n\
             X-Tenant-ID: {}\r\n\
             Content-Length: {content_len}\r\n\
             \r\n\
             {body_str}",
            self.host,
            self.tenant_id,
        );
        stream.write_all(request.as_bytes())?;

        let mut response = String::new();
        stream.read_to_string(&mut response)?;

        // Parse status line
        let status_line = response.lines().next().unwrap_or("");
        let status: u16 = status_line.split_whitespace().nth(1)
            .and_then(|s| s.parse().ok())
            .unwrap_or(500);

        // Split headers / body
        let resp_body = response.split("\r\n\r\n").nth(1).unwrap_or("");

        if status < 200 || status >= 300 {
            return Err(NexusError::Http { status, body: resp_body.to_string() });
        }
        if resp_body.is_empty() {
            return Ok(Value::Null);
        }
        Ok(serde_json::from_str(resp_body)?)
    }

    // ── Agents ──────────────────────────────────────────────────────────────
    pub fn list_agents(&self, page: u32, page_size: u32) -> Result<Value, NexusError> {
        self.request("GET", &format!("/api/agents?page={page}&pageSize={page_size}"), None)
    }

    pub fn get_agent(&self, agent_id: &str) -> Result<Value, NexusError> {
        self.request("GET", &format!("/api/agents/{agent_id}"), None)
    }

    // ── Swarms ───────────────────────────────────────────────────────────────
    pub fn list_swarms(&self) -> Result<Value, NexusError> {
        self.request("GET", "/api/swarms", None)
    }

    // ── Crypto ───────────────────────────────────────────────────────────────
    pub fn kyber_keypair(&self, level: &str) -> Result<Value, NexusError> {
        self.request("POST", &format!("/api/crypto/kyber/keypair?level={level}"), Some(""))
    }

    pub fn shamir_split(&self, secret_b64: &str, threshold: u32, total: u32) -> Result<Value, NexusError> {
        let body = serde_json::json!({
            "secret": secret_b64,
            "threshold": threshold,
            "total": total
        });
        self.request("POST", "/api/crypto/shamir/split", Some(&body.to_string()))
    }

    // ── Health ───────────────────────────────────────────────────────────────
    pub fn health(&self) -> Result<Value, NexusError> {
        self.request("GET", "/health", None)
    }
}

#[derive(Debug, Serialize, Deserialize)]
pub struct AgentModel {
    pub id: String,
    pub name: String,
    pub capability: String,
}
