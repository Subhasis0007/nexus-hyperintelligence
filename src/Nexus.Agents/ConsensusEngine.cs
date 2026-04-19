using Nexus.Models;

namespace Nexus.Agents;

/// <summary>
/// Raft-based consensus engine simulation for swarm coordination.
/// </summary>
public class ConsensusEngine
{
    public enum NodeRole { Follower, Candidate, Leader }
    private NodeRole _role = NodeRole.Follower;
    private string _leaderId = string.Empty;
    private int _currentTerm = 0;
    private readonly string _nodeId;

    public ConsensusEngine(string nodeId)
    {
        _nodeId = nodeId;
    }

    public string NodeId => _nodeId;
    public NodeRole Role => _role;
    public int Term => _currentTerm;

    public void BecomeLeader()
    {
        _role = NodeRole.Leader;
        _leaderId = _nodeId;
        _currentTerm++;
    }

    public void BecomeFollower(string leaderId, int term)
    {
        _role = NodeRole.Follower;
        _leaderId = leaderId;
        _currentTerm = Math.Max(_currentTerm, term);
    }

    public ConsensusResult ProposeAndVote(string proposalId, IEnumerable<ConsensusVote> votes)
    {
        var voteList = votes.ToList();
        var approve = voteList.Count(v => v.Approve);
        var majority = voteList.Count / 2 + 1;
        return new ConsensusResult
        {
            ProposalId = proposalId,
            Approved = approve >= majority,
            ApproveCount = approve,
            RejectCount = voteList.Count - approve,
            TotalVotes = voteList.Count
        };
    }

    public ConsensusVote CreateVote(string proposalId, bool approve, string reason = "")
    {
        return new ConsensusVote
        {
            AgentId = _nodeId,
            ProposalId = proposalId,
            Approve = approve,
            Reason = reason
        };
    }
}
