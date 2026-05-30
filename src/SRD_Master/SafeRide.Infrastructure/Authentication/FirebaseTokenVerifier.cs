using FirebaseAdmin.Auth;
using SafeRide.Application.Features.Auth.Services;

namespace SafeRide.Infrastructure.Authentication;

public class FirebaseTokenVerifier : IFirebaseTokenVerifier
{
    public async Task<FirebaseUserInfo> VerifyAsync(string firebaseIdToken)
    {
        var decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(firebaseIdToken);

        decodedToken.Claims.TryGetValue("email", out var email);
        decodedToken.Claims.TryGetValue("phone_number", out var phone);
        decodedToken.Claims.TryGetValue("name", out var name);
        decodedToken.Claims.TryGetValue("picture", out var picture);

        return new FirebaseUserInfo
        {
            FirebaseUid = decodedToken.Uid,
            Email = email?.ToString(),
            PhoneNumber = phone?.ToString(),
            Name = name?.ToString(),
            Picture = picture?.ToString()
        };
    }
}