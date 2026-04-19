package com.nexus.sdk.models;

import com.fasterxml.jackson.annotation.JsonIgnoreProperties;
import com.fasterxml.jackson.annotation.JsonProperty;

@JsonIgnoreProperties(ignoreUnknown = true)
public class Agent {
    @JsonProperty("id")        public String id;
    @JsonProperty("name")      public String name;
    @JsonProperty("capability") public String capability;
    @JsonProperty("status")    public String status;
    @JsonProperty("tenantId")  public String tenantId;

    @Override public String toString() {
        return "Agent{id='" + id + "', name='" + name + "', capability='" + capability + "'}";
    }
}
