using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Linq;
using ApiPontosTuristicos.DTOs;

[ApiController]
[Route("api/[controller]")]
public class PontosTuristicosController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public  PontosTuristicosController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection");
    }

   [HttpGet]
public ActionResult<IEnumerable<PontoTuristico>> Get()
{
    try
    {
        using (SqlConnection conexao = new SqlConnection(_connectionString))
        {
            conexao.Open();
            var query = @"SELECT pt.id_pt, pt.nome_pt, pt.descricao_pt, pt.id_end,
                         e.id_end, e.logradouro_end, e.numero_end, e.bairro_end, 
                         e.cidade_end, e.uf_end, e.cep_end, e.complemento_end 
                         FROM pontos_turisticos pt 
                         INNER JOIN enderecos e ON pt.id_end = e.id_end  order by pt.id_pt desc";
            
            var pontosTuristicos = conexao.Query<PontoTuristico, Endereco, PontoTuristico>(
                query,
                (pontoTuristico, endereco) =>
                {
                    pontoTuristico.Endereco = endereco;
                    return pontoTuristico;
                },
                splitOn: "id_end"
            );

            return Ok(pontosTuristicos);
        }
    }
    catch (Exception ex)
    {
        // Log do erro para debug
        Console.WriteLine(ex.Message);
        return StatusCode(500, new ErroResponse 
        {
            Erro = "Erro interno do servidor",
            Mensagem = ex.Message,
            Timestamp = DateTime.Now
        } );
    }
}

[HttpGet("search")]
public ActionResult<IEnumerable<PontoTuristico>> Search([FromQuery] string query)
{
    try
    {
        using (SqlConnection conexao = new SqlConnection(_connectionString))
        {
            conexao.Open();
            var searchQuery = @"SELECT pt.id_pt, pt.nome_pt, pt.descricao_pt, pt.id_end,
                             e.id_end, e.logradouro_end, e.numero_end, e.bairro_end, 
                             e.cidade_end, e.uf_end, e.cep_end, e.complemento_end 
                             FROM pontos_turisticos pt 
                             INNER JOIN enderecos e ON pt.id_end = e.id_end
                             WHERE pt.nome_pt LIKE @Query 
                             OR e.cidade_end LIKE @Query
                             OR e.uf_end LIKE @Query
                             ORDER BY pt.id_pt DESC";
            
            var pontosTuristicos = conexao.Query<PontoTuristico, Endereco, PontoTuristico>(
                searchQuery,
                (pontoTuristico, endereco) =>
                {
                    pontoTuristico.Endereco = endereco;
                    return pontoTuristico;
                },
                new { Query = $"%{query}%" },
                splitOn: "id_end"
            );

            return Ok(pontosTuristicos);
        }
    }
    catch (Exception ex)
    {
        return StatusCode(500, new ErroResponse 
        {
            Erro = "Erro interno do servidor",
            Mensagem = ex.Message,
            Timestamp = DateTime.Now
        });
    }
}

    [HttpGet("{id}")]
    public ActionResult<PontoTuristico> Get(int id)
    {
        try
        {
            using (SqlConnection conexao = new SqlConnection(_connectionString))
            {
                conexao.Open();
                var query = @"SELECT pt.*, e.* 
                            FROM pontos_turisticos pt 
                            INNER JOIN enderecos e ON pt.id_end = e.id_end 
                            WHERE pt.id_pt = @Id";
                
                var pontoTuristico = conexao.Query<PontoTuristico, Endereco, PontoTuristico>(
                    query,
                    (pontoTuristico, endereco) =>
                    {
                        pontoTuristico.Endereco = endereco;
                        return pontoTuristico;
                    },
                    new { Id = id },
                    splitOn: "id_end"
                ).FirstOrDefault();

                if (pontoTuristico == null)
                    return NotFound(new {
                        Erro = "id não encontrado",
                        Mensagem = "Ponto turístico não encontrado",
                        Timestamp = DateTime.Now
                    });

                return Ok(pontoTuristico);
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErroResponse
            {
                Erro = "Erro interno do servidor",
                Mensagem = ex.Message,
                Timestamp = DateTime.Now
            });
        }
    }

    [HttpPost]
    public ActionResult<PontoTuristico> Post([FromBody] PontoTuristicoDTO pontoTuristicoDto)
    {
        try
        {
            using (SqlConnection conexao = new SqlConnection(_connectionString))
            {
                conexao.Open();
                using (var transaction = conexao.BeginTransaction())
                {
                    try
                    {
                        var enderecoId = conexao.QuerySingle<int>(
                            @"INSERT INTO enderecos (cep_end, logradouro_end, numero_end, 
                            bairro_end, cidade_end, uf_end, complemento_end) 
                            VALUES (@Cep_end, @Logradouro_end, @Numero_end, @Bairro_end, 
                            @Cidade_end, @Uf_end, @Complemento_end); 
                            SELECT CAST(SCOPE_IDENTITY() as int)",
                            pontoTuristicoDto.Endereco,
                            transaction);

                        var pontoTuristicoId = conexao.QuerySingle<int>(
                            @"INSERT INTO pontos_turisticos (nome_pt, descricao_pt, id_end) 
                            VALUES (@Nome_pt, @Descricao_pt, @Id_end); 
                            SELECT CAST(SCOPE_IDENTITY() as int)",
                            new
                            {
                                pontoTuristicoDto.Nome_pt,
                                pontoTuristicoDto.Descricao_pt,
                                Id_end = enderecoId
                            },
                            transaction);

                        transaction.Commit();
                        return CreatedAtAction(nameof(Get), new { id = pontoTuristicoId }, pontoTuristicoDto);
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErroResponse 
            {
                Erro = "Erro interno do servidor",
                Mensagem = ex.Message,
                Timestamp = DateTime.Now
            });
        }
    }

   
}
