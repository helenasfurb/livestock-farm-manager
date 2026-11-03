using Microsoft.AspNetCore.Identity;

namespace MuuBoi.Application.Helpers
{
    public class PortugueseIdentityErrorDescriber : IdentityErrorDescriber
    {
        public override IdentityError DefaultError() => new()
        {
            Code = nameof(DefaultError),
            Description = "Ocorreu um erro inesperado."
        };

        public override IdentityError ConcurrencyFailure() => new()
        {
            Code = nameof(ConcurrencyFailure),
            Description = "Os dados foram alterados por outra operação. Tente novamente."
        };

        public override IdentityError PasswordMismatch() => new()
        {
            Code = nameof(PasswordMismatch),
            Description = "Senha incorreta."
        };

        public override IdentityError InvalidToken() => new()
        {
            Code = nameof(InvalidToken),
            Description = "Código inválido ou expirado."
        };

        public override IdentityError InvalidEmail(string? email) => new()
        {
            Code = nameof(InvalidEmail),
            Description = $"O e-mail '{email}' é inválido."
        };

        public override IdentityError InvalidUserName(string? userName) => new()
        {
            Code = nameof(InvalidUserName),
            Description = $"O e-mail '{userName}' contém caracteres não permitidos."
        };

        public override IdentityError DuplicateEmail(string email) => new()
        {
            Code = nameof(DuplicateEmail),
            Description = $"O e-mail '{email}' já está cadastrado."
        };

        public override IdentityError DuplicateUserName(string userName) => new()
        {
            Code = nameof(DuplicateUserName),
            Description = $"O e-mail '{userName}' já está cadastrado."
        };

        public override IdentityError PasswordTooShort(int length) => new()
        {
            Code = nameof(PasswordTooShort),
            Description = $"A senha deve ter no mínimo {length} caracteres."
        };

        public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => new()
        {
            Code = nameof(PasswordRequiresUniqueChars),
            Description = $"A senha deve ter pelo menos {uniqueChars} caracteres diferentes."
        };

        public override IdentityError PasswordRequiresNonAlphanumeric() => new()
        {
            Code = nameof(PasswordRequiresNonAlphanumeric),
            Description = "A senha deve ter pelo menos um caractere especial (por exemplo, ! @ # $)."
        };

        public override IdentityError PasswordRequiresDigit() => new()
        {
            Code = nameof(PasswordRequiresDigit),
            Description = "A senha deve ter pelo menos um número (0–9)."
        };

        public override IdentityError PasswordRequiresLower() => new()
        {
            Code = nameof(PasswordRequiresLower),
            Description = "A senha deve ter pelo menos uma letra minúscula (a–z)."
        };

        public override IdentityError PasswordRequiresUpper() => new()
        {
            Code = nameof(PasswordRequiresUpper),
            Description = "A senha deve ter pelo menos uma letra maiúscula (A–Z)."
        };
    }
}
