﻿using ProcessExtensions.Logger.Enums;

namespace ProcessExtensions.Logger
{
    internal class Log(string in_message, ELogLevel in_logLevel)
    {
        public string Message { get; set; } = in_message;

        public ELogLevel LogLevel { get; set; } = in_logLevel;

        public ulong RepeatCount { get; set; }

        public override bool Equals(object? in_obj)
        {
            if (in_obj == null)
                return false;

            if (in_obj.GetType().Equals(typeof(Log)))
            {
                var log = (Log)in_obj;

                return Message  == log.Message &&
                       LogLevel == log.LogLevel;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Message.GetHashCode();
        }
    }
}
