package com.nexus.sdk;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ObjectNode;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;

/**
 * Nexus HyperIntelligence Java SDK Client.
 * Zero external HTTP dependency — uses java.net.HttpURLConnection.
 */
public class NexusClient {

    private final String baseUrl;
    private final String tenantId;
    private final int timeoutMs;
    private final ObjectMapper mapper = new ObjectMapper();

    public NexusClient(String baseUrl, String tenantId) {
        this.baseUrl = baseUrl.replaceAll("/$", "");
        this.tenantId = tenantId;
        this.timeoutMs = 30_000;
    }

    public NexusClient() {
        this("http://localhost:5000", "tenant-default");
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private JsonNode request(String method, String path, Object body) throws IOException, NexusException {
        URL url = new URL(baseUrl + path);
        HttpURLConnection conn = (HttpURLConnection) url.openConnection();
        conn.setRequestMethod(method);
        conn.setRequestProperty("Content-Type", "application/json");
        conn.setRequestProperty("Accept", "application/json");
        conn.setRequestProperty("X-Tenant-ID", tenantId);
        conn.setConnectTimeout(timeoutMs);
        conn.setReadTimeout(timeoutMs);

        if (body != null) {
            conn.setDoOutput(true);
            byte[] data = mapper.writeValueAsBytes(body);
            try (OutputStream os = conn.getOutputStream()) {
                os.write(data);
            }
        }

        int status = conn.getResponseCode();
        InputStream stream = (status < 400) ? conn.getInputStream() : conn.getErrorStream();
        byte[] bytes = (stream != null) ? stream.readAllBytes() : new byte[0];

        if (status < 200 || status >= 300) {
            throw new NexusException(status, new String(bytes, StandardCharsets.UTF_8));
        }
        return bytes.length > 0 ? mapper.readTree(bytes) : mapper.createObjectNode();
    }

    // ── Agents ───────────────────────────────────────────────────────────────

    public JsonNode listAgents(int page, int pageSize) throws IOException, NexusException {
        return request("GET", "/api/agents?page=" + page + "&pageSize=" + pageSize, null);
    }

    public JsonNode getAgent(String agentId) throws IOException, NexusException {
        return request("GET", "/api/agents/" + URLEncoder.encode(agentId, StandardCharsets.UTF_8), null);
    }

    public JsonNode createAgent(String name, String capability) throws IOException, NexusException {
        ObjectNode body = mapper.createObjectNode()
            .put("name", name)
            .put("capability", capability)
            .put("tenantId", tenantId);
        return request("POST", "/api/agents", body);
    }

    // ── Swarms ────────────────────────────────────────────────────────────────

    public JsonNode listSwarms() throws IOException, NexusException {
        return request("GET", "/api/swarms", null);
    }

    public JsonNode runConsensus(String swarmId, String proposalId, String proposal) throws IOException, NexusException {
        ObjectNode body = mapper.createObjectNode()
            .put("proposalId", proposalId)
            .put("proposal", proposal);
        return request("POST", "/api/swarms/" + URLEncoder.encode(swarmId, StandardCharsets.UTF_8) + "/consensus", body);
    }

    // ── Crypto ────────────────────────────────────────────────────────────────

    public JsonNode kyberKeypair(String level) throws IOException, NexusException {
        if (level == null || level.isBlank()) level = "Kyber768";
        return request("POST", "/api/crypto/kyber/keypair?level=" + level, null);
    }

    public JsonNode shamirSplit(String secretB64, int threshold, int total) throws IOException, NexusException {
        ObjectNode body = mapper.createObjectNode()
            .put("secret", secretB64)
            .put("threshold", threshold)
            .put("total", total);
        return request("POST", "/api/crypto/shamir/split", body);
    }

    // ── Health ────────────────────────────────────────────────────────────────

    public JsonNode health() throws IOException, NexusException {
        return request("GET", "/health", null);
    }
}
