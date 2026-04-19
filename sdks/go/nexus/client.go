// Package nexus provides a Go client for the Nexus HyperIntelligence API.
package nexus

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"time"
)

// NexusError represents a non-2xx API response.
type NexusError struct {
	StatusCode int
	Body       string
}

func (e *NexusError) Error() string {
	return fmt.Sprintf("HTTP %d: %s", e.StatusCode, e.Body)
}

// Client is a Nexus API client.
type Client struct {
	baseURL  string
	tenantID string
	http     *http.Client
}

// NewClient creates a new Nexus client.
func NewClient(baseURL, tenantID string) *Client {
	if baseURL == "" {
		baseURL = "http://localhost:5000"
	}
	if tenantID == "" {
		tenantID = "tenant-default"
	}
	return &Client{
		baseURL:  baseURL,
		tenantID: tenantID,
		http:     &http.Client{Timeout: 30 * time.Second},
	}
}

func (c *Client) do(ctx context.Context, method, path string, body any) (json.RawMessage, error) {
	u := c.baseURL + path
	var bodyReader io.Reader
	if body != nil {
		data, err := json.Marshal(body)
		if err != nil {
			return nil, fmt.Errorf("marshal body: %w", err)
		}
		bodyReader = bytes.NewReader(data)
	}
	req, err := http.NewRequestWithContext(ctx, method, u, bodyReader)
	if err != nil {
		return nil, err
	}
	req.Header.Set("Content-Type", "application/json")
	req.Header.Set("Accept", "application/json")
	req.Header.Set("X-Tenant-ID", c.tenantID)

	resp, err := c.http.Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()
	raw, _ := io.ReadAll(resp.Body)
	if resp.StatusCode < 200 || resp.StatusCode >= 300 {
		return nil, &NexusError{StatusCode: resp.StatusCode, Body: string(raw)}
	}
	return raw, nil
}

// ListAgents returns a paged list of agents.
func (c *Client) ListAgents(ctx context.Context, page, pageSize int) (json.RawMessage, error) {
	return c.do(ctx, http.MethodGet,
		fmt.Sprintf("/api/agents?page=%d&pageSize=%d", page, pageSize), nil)
}

// GetAgent returns a single agent by ID.
func (c *Client) GetAgent(ctx context.Context, agentID string) (json.RawMessage, error) {
	return c.do(ctx, http.MethodGet, "/api/agents/"+url.PathEscape(agentID), nil)
}

// CreateAgent creates a new agent.
func (c *Client) CreateAgent(ctx context.Context, name, capability, swarmID string) (json.RawMessage, error) {
	return c.do(ctx, http.MethodPost, "/api/agents", map[string]any{
		"name": name, "capability": capability,
		"tenantId": c.tenantID, "swarmId": swarmID,
	})
}

// ExecuteAgentTask executes a task on an agent.
func (c *Client) ExecuteAgentTask(ctx context.Context, agentID, taskType string, params map[string]any) (json.RawMessage, error) {
	return c.do(ctx, http.MethodPost, "/api/agents/"+url.PathEscape(agentID)+"/execute",
		map[string]any{"taskType": taskType, "parameters": params})
}

// ListSwarms returns all swarms.
func (c *Client) ListSwarms(ctx context.Context) (json.RawMessage, error) {
	return c.do(ctx, http.MethodGet, "/api/swarms", nil)
}

// RunConsensus triggers a consensus round.
func (c *Client) RunConsensus(ctx context.Context, swarmID, proposalID, proposal string) (json.RawMessage, error) {
	return c.do(ctx, http.MethodPost, "/api/swarms/"+url.PathEscape(swarmID)+"/consensus",
		map[string]any{"proposalId": proposalID, "proposal": proposal})
}

// KyberKeypair generates a Kyber keypair.
func (c *Client) KyberKeypair(ctx context.Context, level string) (json.RawMessage, error) {
	if level == "" {
		level = "Kyber768"
	}
	return c.do(ctx, http.MethodPost, "/api/crypto/kyber/keypair?level="+level, nil)
}

// ShamirSplit splits a secret.
func (c *Client) ShamirSplit(ctx context.Context, secretB64 string, threshold, total int) (json.RawMessage, error) {
	return c.do(ctx, http.MethodPost, "/api/crypto/shamir/split",
		map[string]any{"secret": secretB64, "threshold": threshold, "total": total})
}

// Health returns the API health status.
func (c *Client) Health(ctx context.Context) (json.RawMessage, error) {
	return c.do(ctx, http.MethodGet, "/health", nil)
}
