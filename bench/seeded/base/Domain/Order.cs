namespace Shop.Domain;

public sealed record Order(int Id, decimal Total, string CustomerId);
