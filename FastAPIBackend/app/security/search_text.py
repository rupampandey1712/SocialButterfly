"""Literal contains pattern matching the .NET SearchText.Contains helper."""


def contains_pattern(value: str) -> str:
    value=value.strip()[:100]
    escaped=value.replace("\\","\\\\").replace("%","\\%").replace("_","\\_").replace("[","\\[")
    return f"%{escaped}%"
