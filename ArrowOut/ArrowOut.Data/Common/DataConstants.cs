namespace ArrowOut.Data.Common;

// All the length and range limits live here so the database, services and views agree.
public static class DataConstants
{
    public static class Level
    {
        public const int NameMinLength = 3;
        public const int NameMaxLength = 60;
        public const int NumberMin = 1;
        public const int NumberMax = 99_999;
        public const int SizeMin = Game.BoardValidator.MinSize;
        public const int SizeMax = Game.BoardValidator.MaxSize;
        public const int LivesMin = 1;
        public const int LivesMax = 5;

        // Letters, digits, spaces and a bit of punctuation.
        public const string NamePattern = @"^[\p{L}\p{N} '\-\.!?,]+$";
    }

    public static class Arrow
    {
        public const int LengthMin = Game.ArrowPiece.MinLength;
        public const int LengthMax = Game.ArrowPiece.MaxLength;
    }

    public static class Progress
    {
        public const int StarsMax = 3;
    }

    public static class User
    {
        public const int DisplayNameMinLength = 2;
        public const int DisplayNameMaxLength = 30;
        public const string DisplayNamePattern = @"^[\p{L}\p{N} _\-\.]+$";
        public const int ThemeKeyMaxLength = 40;
    }
}
