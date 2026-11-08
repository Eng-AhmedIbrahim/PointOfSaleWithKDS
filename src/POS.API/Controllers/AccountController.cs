namespace POS.API.Controllers;

public class AccountController : BaseApiController
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IAuthService _authService;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly IMapper _mapper;

    public AccountController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager,
        IAuthService authService,
        RoleManager<ApplicationRole> roleManager,
        IConfiguration configuration,
        IMapper mapper
        )
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _authService = authService;
        _roleManager = roleManager;
        _configuration = configuration;
        _mapper = mapper;
    }

    [HttpPost("CreateUser")]
    public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
    {
        if ((await CheckUserExists(registerDto!.UserName!)).Value)
            return BadRequest(new ApiValidationErrorResponse() { Errors = new string[] { "This User already exists!" } });

        AppUser? user = new AppUser
        {
            UserName = registerDto.UserName,
            DisplayName = registerDto.DisplayName ?? string.Empty,
            Email = registerDto.Email,
            RegistrationDate = DateTime.Now,
            DateOfBirth = registerDto.DateOfBirth,
            ImageUrl = registerDto.ImageUrl != null ? registerDto.ImageUrl.FileName : null,
            ArabicName = registerDto.ArabicName,
            PhoneNumber = registerDto.PhoneNumber,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, registerDto!.Password!);

        if (!result.Succeeded)
        {
            Log.Error("Cant create user");
            return BadRequest(new ApiResponse(400, "Cant create user"));
        }

        if (!await _roleManager.RoleExistsAsync(registerDto!.roleName!))
        {
            Log.Error("Cant find role");
            return BadRequest(new ApiResponse(400, "Cant find role"));
        }

        var result1 = await _userManager.AddToRoleAsync(user, registerDto!.roleName!);

        if (!result1.Succeeded)
        {
            Log.Error("Cant add user to role");
            return BadRequest(new ApiResponse(400, "Cant add user to role"));
        }


        return Ok(true);
    }

    [HttpPost("Login")]
    public async Task<ActionResult<UserDto>> Login(LoginDto model)
    {
        if (!ModelState.IsValid)
            return Unauthorized(new ApiResponse(401));

        var user = _userManager.Users.FirstOrDefault(x => x.DisplayName == model.UserName)
                   ?? await _userManager.FindByNameAsync(model.UserName!);
        if (user is null || !user.IsActive)
            return Unauthorized(new ApiResponse(401));

        var result = await _signInManager.PasswordSignInAsync(user, model.Password!, false, false);
        if (!result.Succeeded)
            return Unauthorized(new ApiResponse(401));

        var roles = await _userManager.GetRolesAsync(user);
        var allClaims = new List<Claim>();

        foreach (var role in roles)
        {
            var roleObj = await _roleManager.FindByNameAsync(role);
            if (roleObj != null)
            {
                var roleClaims = await _roleManager.GetClaimsAsync(roleObj);
                // Include both "Permission" and "deny" claims so JWT carries full authorization info
                allClaims.AddRange(roleClaims);
            }
        }

        // Also include user-level claims (if any)
        var userClaims = await _userManager.GetClaimsAsync(user);
        allClaims.AddRange(userClaims);

        var token = await _authService!.CreateTokenAsync(user, roles, allClaims);

        var userDto = _mapper.Map<UserDto>(user);
        userDto.Token = token;
        userDto.Roles = roles.ToList();
        
        // Fetch filtered permissions based on the application type (BackOffice vs POS)
        var filteredPermissions = await _authService.GetUserPermissionsAsync(user, model.ForBackOffice);
        userDto.Permissions = filteredPermissions.ToList();

        return Ok(userDto);
    }



    [Authorize]
    [HttpGet("GetCurrentUser")]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var UserName = User.FindFirstValue(ClaimTypes.Name);

        var user = await _userManager.FindByNameAsync(UserName!);

        return Ok(new UserDto()
        {
            UserName = user!.UserName!,
            //Token = await _authService.CreateTokenAsync(user, _userManager)
        });
    }

    [HttpGet("GetUsers")]
    public async Task<ActionResult<List<UserDto>>> GetUsers()
    {
        var rolesToShow = await _roleManager.Roles.Where(r => r.ShowInLogin).Select(r => r.Name).ToListAsync();
        var activeUsers = await _userManager.Users.Where(u => u.IsActive).ToListAsync();
        
        var filteredUsers = new List<AppUser>();
        foreach(var user in activeUsers)
        {
            var userRoles = await _userManager.GetRolesAsync(user);
            if (userRoles.Any(r => rolesToShow.Contains(r)))
            {
                filteredUsers.Add(user);
            }
        }

        var mappedUsers = _mapper.Map<List<UserDto>>(filteredUsers);
        for (int i = 0; i < filteredUsers.Count; i++)
        {
            if (i < mappedUsers.Count) mappedUsers[i].UserId = filteredUsers[i].Id;
        }
        return Ok(mappedUsers);
    }

    [HttpGet("GetUsersWithRoles")]
    public async Task<ActionResult<List<UserDto>>> GetUsersWithRoles()
    {
        var allUsers = await _userManager.Users.ToListAsync();
        var mappedUsers = new List<UserDto>();

        foreach (var user in allUsers)
        {
            var userRoles = await _userManager.GetRolesAsync(user);
            var mappedUser = _mapper.Map<UserDto>(user);
            mappedUser.UserId = user.Id;
            mappedUser.Roles = userRoles.ToList();
            mappedUsers.Add(mappedUser);
        }

        return Ok(mappedUsers);
    }

    [HttpGet("UserExists")]
    public async Task<ActionResult<bool>> CheckUserExists(string userName)
        => await _userManager.FindByNameAsync(userName) is not null;

    [HttpPost("CreateRole")]
    public async Task<ActionResult> CreateRole(string Name)
    {
        try
        {
            if (string.IsNullOrEmpty(Name)) return BadRequest(new ApiResponse(400, "Role cannot be Empty !!"));

            bool isRoleAlreadyExists = await _roleManager.RoleExistsAsync(Name);
            if (isRoleAlreadyExists) return BadRequest(new ApiResponse(400, $"Role: {Name} Already Exists !!"));

            // Calculate next sequential ID
            var roles = await _roleManager.Roles.ToListAsync();
            int nextId = 1;
            if (roles.Any())
            {
                var numericIds = roles
                    .Select(r => {
                        int.TryParse(r.Id, out int id);
                        return id;
                    })
                    .Where(id => id > 0)
                    .ToList();
                
                if (numericIds.Any())
                {
                    nextId = numericIds.Max() + 1;
                }
            }

            await _roleManager.CreateAsync(new ApplicationRole 
            { 
                Id = nextId.ToString(),
                Name = Name,
                NormalizedName = Name.ToUpper()
            });
            return Ok(Name);
        }
        catch (Exception ex)
        {
            Log.Error(ex.Message);
            return BadRequest(new ApiResponse(400, ex.Message));
        }
    }


    //[Authorize]
    [HttpPost("Logout")]
    public async Task<ActionResult> Logout()
    {
        await _signInManager.SignOutAsync();

        var token = HttpContext.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

        if (token != null)
        {
            try
            {
                return Ok(new { message = "Logged out successfully" });
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message); // Log the error
                return BadRequest(new { message = "Error during logout" });
            }
        }

        return BadRequest(new { message = "Unable to logout" });
    }


    [HttpDelete("delete-role/{roleName}")]
    public async Task<IActionResult> DeleteRole(string roleName)
    {
        var result = await _authService.DeleteRoleAsync(roleName);
        return result ? Ok("Role deleted successfully") : NotFound("Role not found");
    }

    [HttpDelete("delete-user/{userId}")]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        var result = await _authService.DeleteUserAsync(userId);
        return result ? Ok("User deleted successfully") : NotFound("User not found");
    }

    [HttpGet("get-roles")]
    public async Task<ActionResult<List<RoleToReturnDto>>> GetAllRoles()
    {
        var roles = await _authService.GetAllRolesAsync();
        var mappedRoles = _mapper.Map<List<ApplicationRole>, List<RoleToReturnDto>>(roles);
        return Ok(mappedRoles);
    }

    [HttpGet("get-all-permissions")]
    public async Task<ActionResult<List<PermissionDto>>> GetAllPermissions()
    {
        var permissions = await _authService.GetAllPermissionsAsync();
        // Since it's a simple mapping and we might not have it in AutoMapper yet, do it manually or assume AutoMapper covers it
        var mapped = _mapper.Map<List<Permission>, List<PermissionDto>>(permissions);
        return Ok(mapped);
    }

    [HttpGet("get-role-permissions/{roleName}")]
    public async Task<ActionResult<List<string>>> GetRolePermissions(string roleName)
    {
        var permissions = await _authService.GetRolePermissionsAsync(roleName);
        return Ok(permissions);
    }

    [HttpGet("get-users")]
    public async Task<ActionResult<List<UserDto>>> GetAllUsers()
    {
        var users = await _authService.GetAllUsersAsync();
        var mapped = _mapper.Map<List<UserDto>>(users);
        
        for (int i = 0; i < users.Count; i++)
        {
            if (i < mapped.Count) mapped[i].UserId = users[i].Id;
        }

        return Ok(mapped);
    }

    [HttpGet("get-role/{roleName}")]
    public async Task<ActionResult<ApplicationRole>> GetRole(string roleName)
    {
        var role = await _authService.GetRoleAsync(roleName);
        return role != null ? Ok(role) : NotFound("Role not found");
    }

    [HttpGet("get-user/{userId}")]
    public async Task<ActionResult<UserDto>> GetUser(string userId)
    {
        var user = await _authService.GetUserAsync(userId);
        if (user == null) return NotFound();

        var mapped = _mapper.Map<UserDto>(user);
        mapped.UserId = user.Id;
        return Ok(mapped);
    }

    [HttpPost("remove-user-from-role")]
    public async Task<IActionResult> RemoveUserFromRole([FromBody] UserRoleRequest request)
    {
        var result = await _authService.RemoveUserFromRoleAsync(request!.UserId!, request!.RoleName!);
        return result ? Ok("User removed from role successfully") : BadRequest("Failed to remove user from role");
    }

    [HttpPost("add-user-to-role")]
    public async Task<IActionResult> AddUserToRole([FromBody] UserRoleRequest request)
    {
        var result = await _authService.AddUserToRoleAsync(request!.UserId!, request!.RoleName!);
        return result ? Ok("User added to role successfully") : BadRequest("Failed to add user to role");
    }

    [HttpPost("add-claim-to-user")]
    public async Task<IActionResult> AddClaimToUser([FromBody] UserClaimRequest request)
    {
        var user = await _userManager.FindByIdAsync(request!.UserId!);
        if (user == null) return NotFound("User not found");

        var claim = new Claim(request!.ClaimType!, request!.ClaimValue!);
        var result = await _userManager.AddClaimAsync(user, claim);

        return result.Succeeded ? Ok("Claim added successfully") : BadRequest("Failed to add claim");
    }

    [HttpPost("remove-claim-from-user")]
    public async Task<IActionResult> RemoveClaimFromUser([FromBody] UserClaimRequest request)
    {
        var user = await _userManager.FindByIdAsync(request!.UserId!);
        if (user == null) return NotFound("User not found");

        var claim = new Claim(request!.ClaimType!, request!.ClaimValue!);
        var result = await _userManager.RemoveClaimAsync(user, claim);

        return result.Succeeded ? Ok("Claim removed successfully") : BadRequest("Failed to remove claim");
    }

    [HttpGet("get-user-claims/{userId}")]
    public async Task<IActionResult> GetUserClaims(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound("User not found");

        var claims = await _userManager.GetClaimsAsync(user);
        return Ok(claims.Select(c => new { c.Type, c.Value }));
    }


    [HttpGet("user-permissions")]
    public async Task<ActionResult<HashSet<string>>> GetUserPermissions(bool? forBackOffice = null)
    {
        var userPermissions = await _authService.GetUserPermissionsAsync(User, forBackOffice);
        return Ok(userPermissions);
    }




    [HttpPost("seed")]
    public async Task<IActionResult> AddRoleClaims([FromBody] RoleClaimsRequest request)
    {
        if (request == null || request.Roles == null || request.Roles.Count == 0)
        {
            return BadRequest(new ApiResponse(404, "Invalid or empty roles data"));
        }

        foreach (var role in request.Roles)
        {
            var identityRole = await _roleManager.FindByNameAsync(role.Key);
            if (identityRole != null)
            {
                foreach (var permission in role.Value)
                {
                    var existingClaims = await _roleManager.GetClaimsAsync(identityRole);
                    if (!existingClaims.Any(c => c.Type == "Permission" && c.Value == permission))
                    {
                        await _roleManager.AddClaimAsync(identityRole, new Claim("Permission", permission));
                    }
                }
            }
        }

        return Ok(new { message = "Role claims seeded successfully" });
    }

    [HttpPost("update-permissions")]
    public async Task<IActionResult> UpdateRolePermissions([FromBody] RolePermissionsRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.RoleName) || request.Permissions == null || !request.Permissions.Any())
        {
            return BadRequest(new { message = "Invalid request data" });
        }

        var role = await _roleManager.FindByNameAsync(request.RoleName);
        if (role == null)
        {
            return NotFound(new { message = "Role not found" });
        }

        var existingClaims = await _roleManager.GetClaimsAsync(role);

        foreach (var permissionItem in request.Permissions)
        {
            var existingClaim = existingClaims.FirstOrDefault(c => c.Type == "Permission" && c.Value == permissionItem.Permission);

            if (permissionItem.IsGranted)
            {
                // Grant permission if not already assigned
                if (existingClaim == null)
                {
                    await _roleManager.AddClaimAsync(role, new Claim("Permission", permissionItem.Permission));
                }
            }
            else
            {
                // Remove permission if it exists (Deny it)
                if (existingClaim != null)
                {
                    await _roleManager.RemoveClaimAsync(role, existingClaim);
                }
            }
        }

        return Ok(new { message = "Permissions updated successfully" });
    }

    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UserToReturnDto), StatusCodes.Status200OK)]
    [HttpGet("GetUsersByRole/{roleName}")]
    public async Task<IActionResult> GetUsersByRole(string roleName)
    {
        var role = await _roleManager.FindByNameAsync(roleName) ?? await _roleManager.FindByIdAsync(roleName);
        
        if (role is null) return 
                NotFound(new ApiResponse(404, "Role not found"));

        var users = await _authService.GetUsersHasSpecificRole(role.Name!);
        
        if (users is null)
            return NotFound(new ApiResponse(404, "No users found"));

        // Filter only active users
        var activeUsers = users.Where(u => u.IsActive).ToList();
        
        var mappedUsers = _mapper.Map<List<AppUser>, List<UserToReturnDto>>(activeUsers);
        
        return Ok(mappedUsers);
    }

    [HttpPost("UpdateUser")]
    public async Task<IActionResult> UpdateUser(UserDto userDto)
    {
        var user = await _userManager.FindByIdAsync(userDto.UserId!);
        if (user == null) return NotFound(new ApiResponse(404, "User not found"));

        user.UserName = userDto.UserName;
        user.ArabicName = userDto.ArabicName;
        // Don't overwrite DisplayName with null if we aren't editing it
        if (!string.IsNullOrEmpty(userDto.DisplayName))
        {
            user.DisplayName = userDto.DisplayName;
        }
        else if (string.IsNullOrEmpty(user.DisplayName)) 
        {
            user.DisplayName = string.Empty; // Ensure it's never null if DB requires it
        }

        user.PhoneNumber = userDto.PhoneNumber;
        user.IsActive = userDto.IsActive;
        user.DateOfBirth = userDto.DateOfBirth;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errs = string.Join(", ", result.Errors.Select(e => e.Description));
            return BadRequest(new ApiResponse(400, "Failed to update user: " + errs));
        }

        // Update roles
        var currentRoles = await _userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles.Except(userDto.Roles).ToList();
        var rolesToAdd = userDto.Roles.Except(currentRoles).ToList();

        if (rolesToRemove.Any()) await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
        if (rolesToAdd.Any()) await _userManager.AddToRolesAsync(user, rolesToAdd);

        return Ok(true);
    }


    [HttpGet("GetUsersAccountsWithRole")]
    public async Task<ActionResult<List<UserDto>>> GetUsersAccountsWithRole()
    {
        var rolesToShow = await _roleManager.Roles.Where(r => r.ShowInLogin).Select(r => r.Name).ToListAsync();
        var activeUsers = await _userManager.Users.Where(u => u.IsActive).ToListAsync();

        var filteredUsers = new List<AppUser>();
        foreach (var user in activeUsers)
        {
            var userRoles = await _userManager.GetRolesAsync(user);
            if (userRoles.Any(r => rolesToShow.Contains(r)))
            {
                filteredUsers.Add(user);
            }
        }

        var mappedUsers = _mapper.Map<List<UserDto>>(filteredUsers);
        return Ok(mappedUsers);
    }

    [HttpPost("AdminResetPassword")]
    public async Task<IActionResult> AdminResetPassword([FromBody] AdminResetPasswordDto model)
    {
        if (model == null || string.IsNullOrWhiteSpace(model.UserId) || string.IsNullOrWhiteSpace(model.NewPassword))
            return BadRequest(new ApiResponse(400, "User ID and New Password are required"));

        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null)
            return NotFound(new ApiResponse(404, "User not found"));

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return BadRequest(new ApiResponse(400, errors));
        }

        return Ok(true);
    }

    [HttpPost("ChangeOwnPassword")]
    public async Task<IActionResult> ChangeOwnPassword([FromBody] ChangeOwnPasswordDto model)
    {
        if (model == null || string.IsNullOrWhiteSpace(model.CurrentPassword) || string.IsNullOrWhiteSpace(model.NewPassword))
            return BadRequest(new ApiResponse(400, "Current Password and New Password are required"));

        AppUser? user = null;
        if (!string.IsNullOrWhiteSpace(model.UserName))
        {
            user = await _userManager.FindByNameAsync(model.UserName);
        }
        else if (!string.IsNullOrWhiteSpace(model.UserId))
        {
            user = await _userManager.FindByIdAsync(model.UserId);
        }
        else
        {
            var userName = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name;
            if (!string.IsNullOrEmpty(userName))
            {
                user = await _userManager.FindByNameAsync(userName);
            }
        }

        if (user == null)
            return NotFound(new ApiResponse(404, "User not found"));

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return BadRequest(new ApiResponse(400, errors));
        }

        return Ok(true);
    }

}

public class UserRoleRequest
{
    public string? UserId { get; set; }
    public string? RoleName { get; set; }
}

public class UserClaimRequest
{
    public string? UserId { get; set; }
    public string? ClaimType { get; set; }
    public string? ClaimValue { get; set; }
}

public class RoleClaimsRequest
{
    public Dictionary<string, List<string>> Roles { get; set; }
}


public class RolePermissionRequest
{
    public string RoleName { get; set; }
    public string Permission { get; set; }
    public bool IsGranted { get; set; } // true = grant, false = deny (remove)
}




public class RolePermissionItem
{
    public string Permission { get; set; }
    public bool IsGranted { get; set; } // true = grant, false = deny
}

public class RolePermissionsRequest
{
    public string RoleName { get; set; }
    public List<RolePermissionItem> Permissions { get; set; } = new List<RolePermissionItem>();
}


//{
//    "RoleName": "Administrator",
//    "Permissions": [
//        { "Permission": "CanAccessTables", "IsGranted": true },
//        { "Permission": "CanAccessOrders", "IsGranted": false },
//        { "Permission": "CanAccessAccounts", "IsGranted": true }
//    ]
//}
