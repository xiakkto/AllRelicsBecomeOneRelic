extends SceneTree


func _init() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 2:
		push_error("Expected 2 args: <manifest_source_dir> <output_pck>")
		quit(1)
		return

	var manifest_source_dir: String = args[0]
	var output_pck: String = args[1]
	manifest_source_dir = manifest_source_dir.replace("\\", "/")
	output_pck = output_pck.replace("\\", "/")
	var manifest_path := manifest_source_dir.path_join("mod_manifest.json")
	var make_dir_err := DirAccess.make_dir_recursive_absolute(output_pck.get_base_dir())
	if make_dir_err != OK:
		push_error("make_dir_recursive_absolute failed: %s" % make_dir_err)
		quit(1)
		return

	var packer := PCKPacker.new()
	var err := packer.pck_start(output_pck)
	if err != OK:
		push_error("pck_start failed: %s" % err)
		quit(1)
		return

	err = packer.add_file("res://mod_manifest.json", manifest_path)
	if err != OK:
		push_error("add_file failed: %s" % err)
		quit(1)
		return

	err = packer.flush()
	if err != OK:
		push_error("flush failed: %s" % err)
		quit(1)
		return

	quit(0)
