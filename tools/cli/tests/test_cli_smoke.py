from toska_mesh_cli import __version__
from toska_mesh_cli.cli import build_parser, main


def test_build_parser_sets_prog_and_description():
    parser = build_parser()

    assert parser.prog == "toska"
    assert parser.description


def test_info_command_emits_placeholder(capsys):
    exit_code = main(["info"])
    captured = capsys.readouterr()
    output = f"{captured.out}{captured.err}".lower()

    assert exit_code == 0
    assert "placeholder" in output
    assert __version__ in captured.out or __version__ in captured.err
