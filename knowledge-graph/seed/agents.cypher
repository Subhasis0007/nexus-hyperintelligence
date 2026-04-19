// ── Tenant ────────────────────────────────────────────────────────────────────
MERGE (t:Tenant {id: 'tenant-default'})
  ON CREATE SET
    t.name        = 'Default Enterprise Tenant',
    t.tier        = 'Enterprise',
    t.status      = 'Active',
    t.maxAgents   = 200,
    t.maxSwarms   = 16,
    t.createdAt   = datetime();

// ── 16 Swarms ─────────────────────────────────────────────────────────────────
UNWIND [
  {id:'swarm-001', name:'AnalyticsSwarm',      type:'Analytics'},
  {id:'swarm-002', name:'SecuritySwarm',        type:'Security'},
  {id:'swarm-003', name:'DataIngestionSwarm',   type:'DataIngestion'},
  {id:'swarm-004', name:'IntelligenceSwarm',    type:'Intelligence'},
  {id:'swarm-005', name:'PredictionSwarm',      type:'Prediction'},
  {id:'swarm-006', name:'ComplianceSwarm',      type:'Compliance'},
  {id:'swarm-007', name:'MonitoringSwarm',      type:'Monitoring'},
  {id:'swarm-008', name:'OrchestrationSwarm',   type:'Orchestration'},
  {id:'swarm-009', name:'KnowledgeSwarm',       type:'Knowledge'},
  {id:'swarm-010', name:'CommunicationSwarm',   type:'Communication'},
  {id:'swarm-011', name:'OptimizationSwarm',    type:'Optimization'},
  {id:'swarm-012', name:'ResilienceSwarm',      type:'Resilience'},
  {id:'swarm-013', name:'DiscoverySwarm',       type:'Discovery'},
  {id:'swarm-014', name:'TransformationSwarm',  type:'Transformation'},
  {id:'swarm-015', name:'ValidationSwarm',      type:'Validation'},
  {id:'swarm-016', name:'EvolutionSwarm',       type:'Evolution'}
] AS s
MERGE (sw:Swarm {id: s.id})
  ON CREATE SET
    sw.name      = s.name,
    sw.type      = s.type,
    sw.status    = 'Active',
    sw.tenantId  = 'tenant-default',
    sw.createdAt = datetime()
WITH sw
MATCH (t:Tenant {id: 'tenant-default'})
MERGE (sw)-[:BELONGS_TO]->(t);

// ── 200 Agents (12-13 per swarm) ─────────────────────────────────────────────
WITH ['Analytics','Security','DataIngestion','Intelligence','Prediction','Compliance',
      'Monitoring','Orchestration','Knowledge','Communication','Optimization',
      'Resilience','Discovery','Transformation','Validation','Evolution'] AS caps,
     range(1, 200) AS indices
UNWIND indices AS i
WITH i, caps[(i-1) % 16] AS cap,
     'swarm-' + RIGHT('00' + toString(((i-1) % 16) + 1), 3) AS swarmId
MERGE (a:Agent {id: 'agent-' + RIGHT('000' + toString(i), 3)})
  ON CREATE SET
    a.name           = 'AutoAgent_' + RIGHT('000' + toString(i), 3),
    a.capability     = cap,
    a.status         = 'Idle',
    a.tenantId       = 'tenant-default',
    a.swarmId        = swarmId,
    a.confidenceScore = 1.0,
    a.messageCount   = 0,
    a.createdAt      = datetime()
WITH a, swarmId
MATCH (sw:Swarm {id: swarmId})
MERGE (a)-[:BELONGS_TO]->(sw);
