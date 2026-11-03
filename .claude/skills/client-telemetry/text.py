# The forms of the numbers and the tables every report uses.


def seconds(milliseconds):
    return f"{milliseconds / 1000:.2f}s" if milliseconds >= 1000 else f"{milliseconds:.0f}ms"


def table(rows):
    widths = [max(len(row[column]) for row in rows) for column in range(len(rows[0]))]

    return ["  ".join(cell.ljust(width) for cell, width in zip(row, widths)).rstrip() for row in rows]
