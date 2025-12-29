namespace MeshRpcDemo.Contracts;

public record StepBRequest(string Value, string CorrelationId);
public record StepBResponse(string Value);

public record StepCRequest(string Value, string CorrelationId);
public record StepCResponse(string Value);
