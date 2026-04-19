// ── SAP Connector ─────────────────────────────────────────────────────────────
MERGE (c:Connector {id: 'connector-sap-001'})
  ON CREATE SET
    c.name      = 'SAP S/4HANA Connector',
    c.type      = 'SAP',
    c.status    = 'Active',
    c.baseUrl   = 'https://sap-mock.local',
    c.tenantId  = 'tenant-default',
    c.createdAt = datetime();

// ── Salesforce Connector ──────────────────────────────────────────────────────
MERGE (c:Connector {id: 'connector-sf-001'})
  ON CREATE SET
    c.name      = 'Salesforce CRM Connector',
    c.type      = 'Salesforce',
    c.status    = 'Active',
    c.baseUrl   = 'https://sf-mock.salesforce.com',
    c.tenantId  = 'tenant-default',
    c.createdAt = datetime();

// ── ServiceNow Connector ──────────────────────────────────────────────────────
MERGE (c:Connector {id: 'connector-snow-001'})
  ON CREATE SET
    c.name      = 'ServiceNow ITSM Connector',
    c.type      = 'ServiceNow',
    c.status    = 'Active',
    c.baseUrl   = 'https://dev-mock.service-now.com',
    c.tenantId  = 'tenant-default',
    c.createdAt = datetime();

// ── Link connectors to tenant ─────────────────────────────────────────────────
MATCH (t:Tenant {id: 'tenant-default'})
MATCH (c:Connector) WHERE c.tenantId = 'tenant-default'
MERGE (c)-[:BELONGS_TO]->(t);

// ── Knowledge nodes for connector entities ────────────────────────────────────
UNWIND [
  {id:'kn-sap-po',  label:'PurchaseOrder', source:'SAP'},
  {id:'kn-sap-so',  label:'SalesOrder',    source:'SAP'},
  {id:'kn-sf-acct', label:'Account',       source:'Salesforce'},
  {id:'kn-sf-opp',  label:'Opportunity',   source:'Salesforce'},
  {id:'kn-sn-inc',  label:'Incident',      source:'ServiceNow'},
  {id:'kn-sn-chg',  label:'ChangeRequest', source:'ServiceNow'}
] AS kn
MERGE (n:KnowledgeNode {id: kn.id})
  ON CREATE SET
    n.label     = kn.label,
    n.source    = kn.source,
    n.createdAt = datetime();

// ── Link knowledge nodes to connectors ───────────────────────────────────────
MATCH (sap:Connector {id: 'connector-sap-001'})
MATCH (n:KnowledgeNode) WHERE n.source = 'SAP'
MERGE (sap)-[:PROVIDES]->(n);

MATCH (sf:Connector {id: 'connector-sf-001'})
MATCH (n:KnowledgeNode) WHERE n.source = 'Salesforce'
MERGE (sf)-[:PROVIDES]->(n);

MATCH (snow:Connector {id: 'connector-snow-001'})
MATCH (n:KnowledgeNode) WHERE n.source = 'ServiceNow'
MERGE (snow)-[:PROVIDES]->(n);
