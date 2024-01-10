using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AutoMapper;
using Dapper;
using DotnetAPI.Data;
using DotnetAPI.Dtos;
using DotnetAPI.Helpers;
using DotnetAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;

namespace DotnetAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {

        private readonly DataContextDapper _dapper;
        private readonly AuthHelper _authHelper;
        private readonly ReusableSql _reusableSql;
        private readonly IMapper _mapper;

        public AuthController(IConfiguration config)
        {
            _dapper = new DataContextDapper(config);
            _authHelper = new AuthHelper(config);
            _reusableSql = new ReusableSql(config);
            _mapper = new Mapper(new MapperConfiguration(cfg => {
                cfg.CreateMap<UserForRegistrationDto, UserComplete>();
            }));
        }

        [AllowAnonymous]
        [HttpPost("Register")]
        public IActionResult Register(UserForRegistrationDto userForRegistration) {
            if (userForRegistration.Password == userForRegistration.PasswordConfirm){
                string sqlCheckUserExists = @"select Email from TutorialAppSchema.Auth where Email='" 
                    + userForRegistration.Email + "'";
                IEnumerable<string> existingUsers = _dapper.LoadData<string>(sqlCheckUserExists);
                if (existingUsers.Count()==0) {
                    UserForLoginDto userForSetPassword = new UserForLoginDto()
                    {
                        Email = userForRegistration.Email,
                        Password = userForRegistration.Password
                    };

                    if (_authHelper.SetPassword(userForSetPassword)) {
                        UserComplete userComplete = _mapper.Map<UserComplete>(userForRegistration);
                        userComplete.Active = true;
                        
                        if (_reusableSql.UpsertUser(userComplete)){
                            return Ok();
                        }
                        throw new Exception("Failed to add user");
                    }
                    throw new Exception("Failed to register user");
                }
                throw new Exception("User with this email already exists");
            }

            throw new Exception("Passwords do not match");
        }

        [HttpPut("ResetPassword")]
        public IActionResult ResetPassword(UserForLoginDto userForSetPassword)
        {
            if (_authHelper.SetPassword(userForSetPassword))
            {
                return Ok();
            }

            throw new Exception("Failed to update password.");
        }


        [AllowAnonymous]
        [HttpPost("Login")]   
        public IActionResult Login(UserForLoginDto userForLogin)
        {
            string sqlForHashAndSalt = @"EXEC TutorialAppSchema.spLoginConfirmation_get
                @Email=@EmailParam";

            DynamicParameters sqlParameters = new DynamicParameters();
            sqlParameters.Add("@EmailParam", userForLogin.Email, DbType.String);
       

            UserForLoginConfirmationDto userForConfirmation = 
                _dapper.LoadDataSingleLoadDataWithParameters<UserForLoginConfirmationDto>(sqlForHashAndSalt, sqlParameters);

            byte[] passwordHash = _authHelper.GetPasswordHash(userForLogin.Password, userForConfirmation.PasswordSalt);
            
            for (int index=0; index < passwordHash.Length; index++){
                if (passwordHash[index] != userForConfirmation.PasswordHash[index]) {
                    return StatusCode(401, "Incorrect password");
                }
            }

            string sqlUserId = @"SELECT userId FROM TutorialAppSchema.Users 
                WHERE Email='" + userForLogin.Email + "'";

            Console.WriteLine(sqlUserId);

            int userId = _dapper.LoadDataSingle<int>(sqlUserId);
            Console.WriteLine("UserId="+userId);
            return Ok(new Dictionary<string, string> {
                {"token", _authHelper.CreateToken(userId) }
            });
        }

        [HttpGet("RefreshToken")]
        public string RefreshToken() {
            //if userId tied to token is valid. if so, 
            //use it to create new token and send back to user
         
          //the claim created below has user id
           string sqlUserId = @"SELECT userId FROM TutorialAppSchema.Users 
                WHERE userId=" + User.FindFirst("userId")?.Value;
            int userId = _dapper.LoadDataSingle<int>(sqlUserId);

            return _authHelper.CreateToken(userId);

        }
        
    }
}
