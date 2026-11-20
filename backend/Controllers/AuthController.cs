using Finops.Backend.Admin;
using Finops.Backend.Identidade;
using Finops.Backend.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;

namespace Finops.Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UsuarioAtualServico _usuarios;
        private readonly AdminRepositorio _repo;
        private readonly ILogger<AuthController> _logger;

        public AuthController(UsuarioAtualServico usuarios, AdminRepositorio repo, ILogger<AuthController> logger)
        {
            _usuarios = usuarios;
            _repo = repo;
            _logger = logger;
        }

        /// <summary>
        /// Usuário logado. 200 com autorizado=false quando o usuário não existe ou está inativo no
        /// CloudVista (o front mostra "Acesso não autorizado"). 503 sem banco CloudVista.
        /// </summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetMe(CancellationToken ct)
        {
            var identidade = IdentidadeEntra.De(User);
            try
            {
                var usuario = await _usuarios.ObterAsync(ct);
                if (usuario is null)
                {
                    return Ok(new
                    {
                        autorizado = false,
                        nome = User.GetDisplayName(),
                        email = identidade.Email,
                        perfil = (string?)null,
                        usuarioId = (long?)null,
                        clientes = Array.Empty<ClienteResumo>(),
                    });
                }

                var clientes = await _repo.ClientesVisiveisAsync(usuario, ct);
                return Ok(new
                {
                    autorizado = true,
                    nome = string.IsNullOrWhiteSpace(usuario.Nome) ? User.GetDisplayName() : usuario.Nome,
                    email = usuario.Email,
                    perfil = usuario.Perfil,
                    usuarioId = (long?)usuario.Id,
                    clientes,
                });
            }
            catch (CloudVistaNaoConfiguradoException)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { erro = "Banco CloudVista não configurado." });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Falha ao carregar /api/auth/me.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { erro = "Não foi possível validar o acesso. Tente novamente em instantes." });
            }
        }
    }
}
