using MediatR;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Auth.Commands;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;

namespace MotorInsurance.Application.Auth.Queries;

public record GetMeQuery : IRequest<UserProfileDto>;

public class GetMeHandler : IRequestHandler<GetMeQuery, UserProfileDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public GetMeHandler(IAppDbContext db, ICurrentUser currentUser)
        => (_db, _currentUser) = (db, currentUser);

    public async Task<UserProfileDto> Handle(GetMeQuery req, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedException("Not authenticated.");

        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException(nameof(AppUser), userId);

        var roles = user.UserRoles.Select(ur => ur.Role.Code).Distinct().ToList();
        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.PermissionCode))
            .Distinct().ToList();

        return new UserProfileDto(user.Id, user.Username, user.FullName, user.Email, roles, permissions);
    }
}
