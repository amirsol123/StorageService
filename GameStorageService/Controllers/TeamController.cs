using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using AutoMapper;
using GameStorage.Domain;
using GameStorage.Domain.DTOs;
using GameStorage.Domain.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameStorageService.Controllers
{
    [ApiController]
    [Route("/api/[Controller]")]
    public class TeamController : Controller
    {
        private readonly RepositoryWrapper _repositoryWrapper;
        private readonly IMapper _mapper;

        public TeamController(RepositoryWrapper repositoryWrapper, IMapper mapper)
        {
            _repositoryWrapper = repositoryWrapper;
            _mapper = mapper;
        }
        
        public IEnumerable<Team> Index()
        {
            //GetTeam the first ten teams depending on the rating
            var teams = _repositoryWrapper.TeamRepository.GetListQueryable
                .Include(t => t.Config)
                .Include(t => t.GamesWon)
                .Take(10)
                .ToList();
            return teams;
        }

        [HttpPost("create")]
        public IActionResult Register(Team team)
        {
            var teams = _repositoryWrapper
                .TeamRepository
                .FindByExpression(x => x.Name == team.Name || x.Code == team.Code).ToList();
            var cfg = team.Config;
            //check whether there are teams with the same names of same router configs
            if (teams.Count() != 0 ||
                _repositoryWrapper.ConfigRepository.IsIpAndPortUsed(cfg.RouterIpAddress, cfg.RouterPort))
                return Conflict();
            
            //unix requires special permission for port below 1024 and below
            if (team.Config.RouterPort < 1025) return BadRequest();

            //create new team
            var createdTeam = _repositoryWrapper.TeamRepository.CreateNew(team.Name, team.Config);
            _repositoryWrapper.UpdateDB();
            //redirect to the GET method
            return CreatedAtAction(nameof(GetByName), new {teamName = createdTeam.Name },createdTeam);
        }

        //Update only core properties of the team object
        //To update config, use UpdateConfig
        [HttpPut("update/{code}")]
        public IActionResult Update(string code, [FromBody]TeamDTO updatedTeam)
        {
            if (code != updatedTeam.Code) return NotFound();

            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid objects properties");
            }

            var teamEntity = _repositoryWrapper.TeamRepository.GetListQueryable
                .AsNoTracking().FirstOrDefault(x => x.Code == code);
            if (teamEntity == null) return NotFound();
            _mapper.Map(updatedTeam, teamEntity);

            _repositoryWrapper.TeamRepository.Update(teamEntity);
            _repositoryWrapper.UpdateDB();

            return NoContent();
        }

        [HttpPut("config/update/{code}")]
        public IActionResult UpdateConfig(string code, [FromBody] ConfigDTO updatedConfig)
        {
            //find the team according to its code
            var team = _repositoryWrapper
                .TeamRepository
                .GetListQueryable
                .Include(t => t.Config)
                .FirstOrDefault(x => x.Code == code);
            if (team == null) return NotFound();
            
            //check whether the port and ip provided by DTO object are already used
            var isSuccessful = _repositoryWrapper.ConfigRepository.CheckObject(updatedConfig);
            if (!isSuccessful) return Conflict();
            
            //map DTO values to team.config values
            _mapper.Map(updatedConfig, team.Config);

            _repositoryWrapper.TeamRepository.Update(team);
            
            _repositoryWrapper.UpdateDB();
            return NoContent();
        }

        [HttpGet("name/{teamName}")]
        public IActionResult GetByName(string teamName)
        {
            var team = _repositoryWrapper.TeamRepository.FindByName(teamName);
            if (team == null) return NotFound();
            return Ok(team);
        }

        [HttpGet("code/{code}")]
        public IActionResult GetByCode(string code)
        {
            var team = _repositoryWrapper.TeamRepository.FindByCode(code);
            if (team == null) return NotFound();
            return Ok(team);
        }
    }
}