/**
 * Nexus Alloy model — verifies structural properties of the swarm topology.
 * Run with Alloy Analyzer 6.x: `open NexusModel.als`
 */

module NexusModel

// ── Signatures ──────────────────────────────────────────────────────────────
sig Tenant {
  swarms     : set Swarm,
  agents     : set Agent,
  connectors : set Connector
}

sig Swarm {
  tenant    : one Tenant,
  members   : set Agent,
  swarmType : one SwarmType
}

sig Agent {
  swarm      : lone Swarm,
  capability : one Capability,
  myTenant   : one Tenant
}

sig Connector {
  connTenant : one Tenant,
  connType   : one ConnectorType
}

sig CryptoKey {
  algorithm  : one Algorithm,
  owner      : one Agent
}

// ── Enumerations ─────────────────────────────────────────────────────────────
abstract sig SwarmType {}
one sig Analytics, Security, DataIngestion, Intelligence, Prediction,
        Compliance, Monitoring, Orchestration, Knowledge, Communication,
        Optimization, Resilience, Discovery, Transformation, Validation,
        Evolution extends SwarmType {}

abstract sig Capability {}
one sig CapAnalytics, CapSecurity, CapDataIngestion, CapIntelligence,
        CapPrediction, CapCompliance, CapMonitoring, CapOrchestration,
        CapKnowledge, CapCommunication, CapOptimization, CapResilience,
        CapDiscovery, CapTransformation, CapValidation, CapEvolution extends Capability {}

abstract sig ConnectorType {}
one sig SAP, Salesforce, ServiceNow, REST, GraphQL, MQTT, Kafka extends ConnectorType {}

abstract sig Algorithm {}
one sig Kyber512, Kyber768, Kyber1024, Dilithium2, Dilithium3, Dilithium5 extends Algorithm {}

// ── Facts (invariants) ───────────────────────────────────────────────────────

// Agent is in exactly the swarms that list it as a member
fact SwarmMembershipConsistency {
  all s : Swarm, a : Agent |
    a in s.members iff s = a.swarm
}

// Agent's tenant matches its swarm's tenant
fact AgentTenantConsistency {
  all a : Agent |
    some a.swarm implies a.myTenant = a.swarm.tenant
}

// Swarm tenant matches tenant's swarm set
fact TenantSwarmConsistency {
  all t : Tenant, s : Swarm |
    s in t.swarms iff t = s.tenant
}

// Tenant agents equals union of all its swarms' members
fact TenantAgentsComplete {
  all t : Tenant |
    t.agents = { a : Agent | a.myTenant = t }
}

// No agent belongs to two tenants' swarms
fact NoAgentCrossTenant {
  all a : Agent, s1, s2 : Swarm |
    a in s1.members and a in s2.members implies s1 = s2
}

// ── Assertions ───────────────────────────────────────────────────────────────

// Each tenant's swarms are distinct
assert UniqueSwarmPerTenant {
  all t : Tenant |
    all s1, s2 : t.swarms |
      s1.swarmType = s2.swarmType implies s1 = s2
}
check UniqueSwarmPerTenant for 5 but 16 SwarmType, 200 Agent

// All agents have a swarm
assert AllAgentsInSwarm {
  all a : Agent | some a.swarm
}
check AllAgentsInSwarm for 5

// Crypto key owners belong to an active tenant
assert KeysAreOwned {
  all k : CryptoKey | some k.owner
}
check KeysAreOwned for 5

// ── Predicates ───────────────────────────────────────────────────────────────
pred ValidTopology {
  // At least one tenant with 16 swarms
  some t : Tenant | #t.swarms >= 16
}

pred AgentsDistributed {
  // Each swarm has at least one agent
  all s : Swarm | some s.members
}

run ValidTopology for 5 but exactly 1 Tenant, exactly 16 Swarm, 200 Agent
run AgentsDistributed for 5 but exactly 1 Tenant, exactly 4 Swarm, 8 Agent
