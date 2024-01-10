using System.Data;
using Dapper;
using DotnetAPI.Data;
using DotnetAPI.Dtos;
using DotnetAPI.Helpers;
using DotnetAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotnetAPI.Controllers;

[Authorize]
[ApiController]
[Route("[controller]")]
public class UserCompleteController : ControllerBase
{
    private readonly  DataContextDapper _dapper;
    private readonly ReusableSql _reusableSql;

    public UserCompleteController(IConfiguration config)
    {
        _dapper = new DataContextDapper(config);
        _reusableSql = new ReusableSql(config);
    }

    [HttpGet("TestConnection")]
    public DateTime TestConnection()
    {
        return _dapper.LoadDataSingle<DateTime>("SELECT GETDATE()");
    }

    [HttpGet("GetUsers/{userId}/{isActive}")]
    public IEnumerable<UserComplete> GetUsers(int userId, bool isActive)
    {
        string sql = @"EXEC TutorialAppSchema.spUsers_Get";
        string parameters = "";

        DynamicParameters sqlParameters = new DynamicParameters();
        
        if (userId != 0)
        {
            parameters += ", @userId = @UserIdParam";
            sqlParameters.Add("@UserIdParam", userId, DbType.Int32);
        }
        if (isActive)
        {
            parameters += ", @Active= @ActiveParam";
            sqlParameters.Add("@ActiveParam", isActive, DbType.Boolean);
        }

        if (parameters.Length>0) {
            sql += parameters.Substring(1);
        }        

        return _dapper.LoadDataWithParameters<UserComplete>(sql, sqlParameters);
    }

    [HttpPut("UpsertUser")]
    public IActionResult UpsertUser(UserComplete user)
    {
        if (_reusableSql.UpsertUser(user))
        {
            return Ok();
        }
        throw new Exception("Failed to update User");
    }

    [HttpDelete("DeleteUser/userId")]
    public IActionResult DeleteUser(int userId)
    {

        string sql = @"EXEC TutorialAppSchema.spUser_Delete 
           @userId=@UserIdParam";

        DynamicParameters sqlParameters = new DynamicParameters();
        sqlParameters.Add("@UserIdParam", userId, DbType.Int32);

        if (_dapper.ExecuteSqlWithParameters(sql, sqlParameters))
        {
            return Ok();
        }
        throw new Exception("Failed to delete User");
    }

}
 
   
