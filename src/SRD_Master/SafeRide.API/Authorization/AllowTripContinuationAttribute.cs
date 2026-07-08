using SafeRide.Application.Features.Auth;

namespace SafeRide.API.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class AllowTripContinuationAttribute : Attribute
{
    public AllowTripContinuationAttribute(TripContinuationOperation operation)
    {
        Operation = operation;
    }

    public TripContinuationOperation Operation { get; }
}
