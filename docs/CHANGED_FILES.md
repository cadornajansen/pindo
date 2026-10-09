# Changed files

Compared with `origin/main`. A = created; M = modified. No tracked files are deleted. Ignored local build assets and authoring scratch scripts are not part of the change.

461 files are listed individually. Most are independently editable task and evaluation data; the generated app resource duplicates them intentionally for packaging.

| Change | File | Purpose |
| --- | --- | --- |
| A | [.github/workflows/skills.yml](../.github/workflows/skills.yml) | Windows validation plus native macOS build, Swift tests and bundle check. |
| M | [PinDo/QuickBar.swift](../PinDo/QuickBar.swift) | Adds explicit Teach routing and preserves the panel during a lesson. |
| A | [PinDo/Resources/ApplicationSkills.json](../PinDo/Resources/ApplicationSkills.json) | Generated deterministic resource containing all profiles and tasks. |
| A | [PinDo/SkillLibrary.swift](../PinDo/SkillLibrary.swift) | Swift data reader, application/task matching and progression predicates. |
| A | [PinDo/TutorClient.swift](../PinDo/TutorClient.swift) | Bounded local vision request, structured guidance and response metrics. |
| A | [PinDo/TutorObservation.swift](../PinDo/TutorObservation.swift) | Single-window capture, metadata identity and activity notifications. |
| A | [PinDo/TutorSession.swift](../PinDo/TutorSession.swift) | Teaching state, debounce, cancellation, stale responses and progression. |
| A | [PinDo/TutorView.swift](../PinDo/TutorView.swift) | Lesson selection, prerequisites, explanations and pause/confirmation controls. |
| M | [README.md](../README.md) | Teach-mode overview and entry points for development and Mac testing. |
| A | [application_profiles.json](../application_profiles.json) | Application aliases, known bundle IDs, surfaces, domains and terminology. |
| A | [docs/APPLICATION_SKILLS.md](../docs/APPLICATION_SKILLS.md) | Version 2 contract, execution flow, observation limits and evidence policy. |
| A | [docs/CHANGED_FILES.md](../docs/CHANGED_FILES.md) | Complete created/modified file report for this branch. |
| A | [docs/MAC_HANDOFF.md](../docs/MAC_HANDOFF.md) | Exact continuation/build steps and available-app validation instructions. |
| A | [docs/SKILL_CATALOGUE.md](../docs/SKILL_CATALOGUE.md) | Task-by-task inventory and vendor references. |
| A | [evaluations/cases/after_effects_alpha_export_english.json](../evaluations/cases/after_effects_alpha_export_english.json) | English evaluation specification for after_effects.alpha_export. |
| A | [evaluations/cases/after_effects_alpha_export_recovery.json](../evaluations/cases/after_effects_alpha_export_recovery.json) | Recovery evaluation specification for after_effects.alpha_export. |
| A | [evaluations/cases/after_effects_alpha_export_taglish.json](../evaluations/cases/after_effects_alpha_export_taglish.json) | Taglish evaluation specification for after_effects.alpha_export. |
| A | [evaluations/cases/after_effects_composition_english.json](../evaluations/cases/after_effects_composition_english.json) | English evaluation specification for after_effects.composition. |
| A | [evaluations/cases/after_effects_composition_recovery.json](../evaluations/cases/after_effects_composition_recovery.json) | Recovery evaluation specification for after_effects.composition. |
| A | [evaluations/cases/after_effects_composition_taglish.json](../evaluations/cases/after_effects_composition_taglish.json) | Taglish evaluation specification for after_effects.composition. |
| A | [evaluations/cases/after_effects_graph_editor_english.json](../evaluations/cases/after_effects_graph_editor_english.json) | English evaluation specification for after_effects.graph_editor. |
| A | [evaluations/cases/after_effects_graph_editor_recovery.json](../evaluations/cases/after_effects_graph_editor_recovery.json) | Recovery evaluation specification for after_effects.graph_editor. |
| A | [evaluations/cases/after_effects_graph_editor_taglish.json](../evaluations/cases/after_effects_graph_editor_taglish.json) | Taglish evaluation specification for after_effects.graph_editor. |
| A | [evaluations/cases/after_effects_keyframes_easing_english.json](../evaluations/cases/after_effects_keyframes_easing_english.json) | English evaluation specification for after_effects.keyframes_easing. |
| A | [evaluations/cases/after_effects_keyframes_easing_recovery.json](../evaluations/cases/after_effects_keyframes_easing_recovery.json) | Recovery evaluation specification for after_effects.keyframes_easing. |
| A | [evaluations/cases/after_effects_keyframes_easing_taglish.json](../evaluations/cases/after_effects_keyframes_easing_taglish.json) | Taglish evaluation specification for after_effects.keyframes_easing. |
| A | [evaluations/cases/after_effects_motion_tracking_english.json](../evaluations/cases/after_effects_motion_tracking_english.json) | English evaluation specification for after_effects.motion_tracking. |
| A | [evaluations/cases/after_effects_motion_tracking_recovery.json](../evaluations/cases/after_effects_motion_tracking_recovery.json) | Recovery evaluation specification for after_effects.motion_tracking. |
| A | [evaluations/cases/after_effects_motion_tracking_taglish.json](../evaluations/cases/after_effects_motion_tracking_taglish.json) | Taglish evaluation specification for after_effects.motion_tracking. |
| A | [evaluations/cases/after_effects_parenting_english.json](../evaluations/cases/after_effects_parenting_english.json) | English evaluation specification for after_effects.parenting. |
| A | [evaluations/cases/after_effects_parenting_recovery.json](../evaluations/cases/after_effects_parenting_recovery.json) | Recovery evaluation specification for after_effects.parenting. |
| A | [evaluations/cases/after_effects_parenting_taglish.json](../evaluations/cases/after_effects_parenting_taglish.json) | Taglish evaluation specification for after_effects.parenting. |
| A | [evaluations/cases/after_effects_precompose_english.json](../evaluations/cases/after_effects_precompose_english.json) | English evaluation specification for after_effects.precompose. |
| A | [evaluations/cases/after_effects_precompose_recovery.json](../evaluations/cases/after_effects_precompose_recovery.json) | Recovery evaluation specification for after_effects.precompose. |
| A | [evaluations/cases/after_effects_precompose_taglish.json](../evaluations/cases/after_effects_precompose_taglish.json) | Taglish evaluation specification for after_effects.precompose. |
| A | [evaluations/cases/after_effects_track_matte_english.json](../evaluations/cases/after_effects_track_matte_english.json) | English evaluation specification for after_effects.track_matte. |
| A | [evaluations/cases/after_effects_track_matte_recovery.json](../evaluations/cases/after_effects_track_matte_recovery.json) | Recovery evaluation specification for after_effects.track_matte. |
| A | [evaluations/cases/after_effects_track_matte_taglish.json](../evaluations/cases/after_effects_track_matte_taglish.json) | Taglish evaluation specification for after_effects.track_matte. |
| A | [evaluations/cases/canva_audio_timing_english.json](../evaluations/cases/canva_audio_timing_english.json) | English evaluation specification for canva.audio_timing. |
| A | [evaluations/cases/canva_audio_timing_recovery.json](../evaluations/cases/canva_audio_timing_recovery.json) | Recovery evaluation specification for canva.audio_timing. |
| A | [evaluations/cases/canva_audio_timing_taglish.json](../evaluations/cases/canva_audio_timing_taglish.json) | Taglish evaluation specification for canva.audio_timing. |
| A | [evaluations/cases/canva_branding_english.json](../evaluations/cases/canva_branding_english.json) | English evaluation specification for canva.branding. |
| A | [evaluations/cases/canva_branding_recovery.json](../evaluations/cases/canva_branding_recovery.json) | Recovery evaluation specification for canva.branding. |
| A | [evaluations/cases/canva_branding_taglish.json](../evaluations/cases/canva_branding_taglish.json) | Taglish evaluation specification for canva.branding. |
| A | [evaluations/cases/canva_export_images_english.json](../evaluations/cases/canva_export_images_english.json) | English evaluation specification for canva.export_images. |
| A | [evaluations/cases/canva_export_images_recovery.json](../evaluations/cases/canva_export_images_recovery.json) | Recovery evaluation specification for canva.export_images. |
| A | [evaluations/cases/canva_export_images_taglish.json](../evaluations/cases/canva_export_images_taglish.json) | Taglish evaluation specification for canva.export_images. |
| A | [evaluations/cases/canva_export_pdf_english.json](../evaluations/cases/canva_export_pdf_english.json) | English evaluation specification for canva.export_pdf. |
| A | [evaluations/cases/canva_export_pdf_recovery.json](../evaluations/cases/canva_export_pdf_recovery.json) | Recovery evaluation specification for canva.export_pdf. |
| A | [evaluations/cases/canva_export_pdf_taglish.json](../evaluations/cases/canva_export_pdf_taglish.json) | Taglish evaluation specification for canva.export_pdf. |
| A | [evaluations/cases/canva_frames_crop_english.json](../evaluations/cases/canva_frames_crop_english.json) | English evaluation specification for canva.frames_crop. |
| A | [evaluations/cases/canva_frames_crop_recovery.json](../evaluations/cases/canva_frames_crop_recovery.json) | Recovery evaluation specification for canva.frames_crop. |
| A | [evaluations/cases/canva_frames_crop_taglish.json](../evaluations/cases/canva_frames_crop_taglish.json) | Taglish evaluation specification for canva.frames_crop. |
| A | [evaluations/cases/canva_layers_align_english.json](../evaluations/cases/canva_layers_align_english.json) | English evaluation specification for canva.layers_align. |
| A | [evaluations/cases/canva_layers_align_recovery.json](../evaluations/cases/canva_layers_align_recovery.json) | Recovery evaluation specification for canva.layers_align. |
| A | [evaluations/cases/canva_layers_align_taglish.json](../evaluations/cases/canva_layers_align_taglish.json) | Taglish evaluation specification for canva.layers_align. |
| A | [evaluations/cases/canva_multipage_design_english.json](../evaluations/cases/canva_multipage_design_english.json) | English evaluation specification for canva.multipage_design. |
| A | [evaluations/cases/canva_multipage_design_recovery.json](../evaluations/cases/canva_multipage_design_recovery.json) | Recovery evaluation specification for canva.multipage_design. |
| A | [evaluations/cases/canva_multipage_design_taglish.json](../evaluations/cases/canva_multipage_design_taglish.json) | Taglish evaluation specification for canva.multipage_design. |
| A | [evaluations/cases/canva_timed_titles_english.json](../evaluations/cases/canva_timed_titles_english.json) | English evaluation specification for canva.timed_titles. |
| A | [evaluations/cases/canva_timed_titles_recovery.json](../evaluations/cases/canva_timed_titles_recovery.json) | Recovery evaluation specification for canva.timed_titles. |
| A | [evaluations/cases/canva_timed_titles_taglish.json](../evaluations/cases/canva_timed_titles_taglish.json) | Taglish evaluation specification for canva.timed_titles. |
| A | [evaluations/cases/capcut_arrange_clips_english.json](../evaluations/cases/capcut_arrange_clips_english.json) | English evaluation specification for capcut.arrange_clips. |
| A | [evaluations/cases/capcut_arrange_clips_recovery.json](../evaluations/cases/capcut_arrange_clips_recovery.json) | Recovery evaluation specification for capcut.arrange_clips. |
| A | [evaluations/cases/capcut_arrange_clips_taglish.json](../evaluations/cases/capcut_arrange_clips_taglish.json) | Taglish evaluation specification for capcut.arrange_clips. |
| A | [evaluations/cases/capcut_captions_english.json](../evaluations/cases/capcut_captions_english.json) | English evaluation specification for capcut.captions. |
| A | [evaluations/cases/capcut_captions_recovery.json](../evaluations/cases/capcut_captions_recovery.json) | Recovery evaluation specification for capcut.captions. |
| A | [evaluations/cases/capcut_captions_taglish.json](../evaluations/cases/capcut_captions_taglish.json) | Taglish evaluation specification for capcut.captions. |
| A | [evaluations/cases/capcut_color_correction_english.json](../evaluations/cases/capcut_color_correction_english.json) | English evaluation specification for capcut.color_correction. |
| A | [evaluations/cases/capcut_color_correction_recovery.json](../evaluations/cases/capcut_color_correction_recovery.json) | Recovery evaluation specification for capcut.color_correction. |
| A | [evaluations/cases/capcut_color_correction_taglish.json](../evaluations/cases/capcut_color_correction_taglish.json) | Taglish evaluation specification for capcut.color_correction. |
| A | [evaluations/cases/capcut_export_english.json](../evaluations/cases/capcut_export_english.json) | English evaluation specification for capcut.export. |
| A | [evaluations/cases/capcut_export_recovery.json](../evaluations/cases/capcut_export_recovery.json) | Recovery evaluation specification for capcut.export. |
| A | [evaluations/cases/capcut_export_taglish.json](../evaluations/cases/capcut_export_taglish.json) | Taglish evaluation specification for capcut.export. |
| A | [evaluations/cases/capcut_import_trim_english.json](../evaluations/cases/capcut_import_trim_english.json) | English evaluation specification for capcut.import_trim. |
| A | [evaluations/cases/capcut_import_trim_recovery.json](../evaluations/cases/capcut_import_trim_recovery.json) | Recovery evaluation specification for capcut.import_trim. |
| A | [evaluations/cases/capcut_import_trim_taglish.json](../evaluations/cases/capcut_import_trim_taglish.json) | Taglish evaluation specification for capcut.import_trim. |
| A | [evaluations/cases/capcut_keyframes_english.json](../evaluations/cases/capcut_keyframes_english.json) | English evaluation specification for capcut.keyframes. |
| A | [evaluations/cases/capcut_keyframes_recovery.json](../evaluations/cases/capcut_keyframes_recovery.json) | Recovery evaluation specification for capcut.keyframes. |
| A | [evaluations/cases/capcut_keyframes_taglish.json](../evaluations/cases/capcut_keyframes_taglish.json) | Taglish evaluation specification for capcut.keyframes. |
| A | [evaluations/cases/capcut_speed_ramp_english.json](../evaluations/cases/capcut_speed_ramp_english.json) | English evaluation specification for capcut.speed_ramp. |
| A | [evaluations/cases/capcut_speed_ramp_recovery.json](../evaluations/cases/capcut_speed_ramp_recovery.json) | Recovery evaluation specification for capcut.speed_ramp. |
| A | [evaluations/cases/capcut_speed_ramp_taglish.json](../evaluations/cases/capcut_speed_ramp_taglish.json) | Taglish evaluation specification for capcut.speed_ramp. |
| A | [evaluations/cases/capcut_tracked_blur_english.json](../evaluations/cases/capcut_tracked_blur_english.json) | English evaluation specification for capcut.tracked_blur. |
| A | [evaluations/cases/capcut_tracked_blur_recovery.json](../evaluations/cases/capcut_tracked_blur_recovery.json) | Recovery evaluation specification for capcut.tracked_blur. |
| A | [evaluations/cases/capcut_tracked_blur_taglish.json](../evaluations/cases/capcut_tracked_blur_taglish.json) | Taglish evaluation specification for capcut.tracked_blur. |
| A | [evaluations/cases/excel_absolute_references_english.json](../evaluations/cases/excel_absolute_references_english.json) | English evaluation specification for excel.absolute_references. |
| A | [evaluations/cases/excel_absolute_references_recovery.json](../evaluations/cases/excel_absolute_references_recovery.json) | Recovery evaluation specification for excel.absolute_references. |
| A | [evaluations/cases/excel_absolute_references_taglish.json](../evaluations/cases/excel_absolute_references_taglish.json) | Taglish evaluation specification for excel.absolute_references. |
| A | [evaluations/cases/excel_chart_english.json](../evaluations/cases/excel_chart_english.json) | English evaluation specification for excel.chart. |
| A | [evaluations/cases/excel_chart_recovery.json](../evaluations/cases/excel_chart_recovery.json) | Recovery evaluation specification for excel.chart. |
| A | [evaluations/cases/excel_chart_taglish.json](../evaluations/cases/excel_chart_taglish.json) | Taglish evaluation specification for excel.chart. |
| A | [evaluations/cases/excel_clean_csv_english.json](../evaluations/cases/excel_clean_csv_english.json) | English evaluation specification for excel.clean_csv. |
| A | [evaluations/cases/excel_clean_csv_recovery.json](../evaluations/cases/excel_clean_csv_recovery.json) | Recovery evaluation specification for excel.clean_csv. |
| A | [evaluations/cases/excel_clean_csv_taglish.json](../evaluations/cases/excel_clean_csv_taglish.json) | Taglish evaluation specification for excel.clean_csv. |
| A | [evaluations/cases/excel_conditional_formula_english.json](../evaluations/cases/excel_conditional_formula_english.json) | English evaluation specification for excel.conditional_formula. |
| A | [evaluations/cases/excel_conditional_formula_recovery.json](../evaluations/cases/excel_conditional_formula_recovery.json) | Recovery evaluation specification for excel.conditional_formula. |
| A | [evaluations/cases/excel_conditional_formula_taglish.json](../evaluations/cases/excel_conditional_formula_taglish.json) | Taglish evaluation specification for excel.conditional_formula. |
| A | [evaluations/cases/excel_dropdown_english.json](../evaluations/cases/excel_dropdown_english.json) | English evaluation specification for excel.dropdown. |
| A | [evaluations/cases/excel_dropdown_recovery.json](../evaluations/cases/excel_dropdown_recovery.json) | Recovery evaluation specification for excel.dropdown. |
| A | [evaluations/cases/excel_dropdown_taglish.json](../evaluations/cases/excel_dropdown_taglish.json) | Taglish evaluation specification for excel.dropdown. |
| A | [evaluations/cases/excel_lookup_english.json](../evaluations/cases/excel_lookup_english.json) | English evaluation specification for excel.lookup. |
| A | [evaluations/cases/excel_lookup_recovery.json](../evaluations/cases/excel_lookup_recovery.json) | Recovery evaluation specification for excel.lookup. |
| A | [evaluations/cases/excel_lookup_taglish.json](../evaluations/cases/excel_lookup_taglish.json) | Taglish evaluation specification for excel.lookup. |
| A | [evaluations/cases/excel_pivot_table_english.json](../evaluations/cases/excel_pivot_table_english.json) | English evaluation specification for excel.pivot_table. |
| A | [evaluations/cases/excel_pivot_table_recovery.json](../evaluations/cases/excel_pivot_table_recovery.json) | Recovery evaluation specification for excel.pivot_table. |
| A | [evaluations/cases/excel_pivot_table_taglish.json](../evaluations/cases/excel_pivot_table_taglish.json) | Taglish evaluation specification for excel.pivot_table. |
| A | [evaluations/cases/excel_totals_averages_english.json](../evaluations/cases/excel_totals_averages_english.json) | English evaluation specification for excel.totals_averages. |
| A | [evaluations/cases/excel_totals_averages_recovery.json](../evaluations/cases/excel_totals_averages_recovery.json) | Recovery evaluation specification for excel.totals_averages. |
| A | [evaluations/cases/excel_totals_averages_taglish.json](../evaluations/cases/excel_totals_averages_taglish.json) | Taglish evaluation specification for excel.totals_averages. |
| A | [evaluations/cases/figma_components_variants_english.json](../evaluations/cases/figma_components_variants_english.json) | English evaluation specification for figma.components_variants. |
| A | [evaluations/cases/figma_components_variants_recovery.json](../evaluations/cases/figma_components_variants_recovery.json) | Recovery evaluation specification for figma.components_variants. |
| A | [evaluations/cases/figma_components_variants_taglish.json](../evaluations/cases/figma_components_variants_taglish.json) | Taglish evaluation specification for figma.components_variants. |
| A | [evaluations/cases/figma_export_assets_english.json](../evaluations/cases/figma_export_assets_english.json) | English evaluation specification for figma.export_assets. |
| A | [evaluations/cases/figma_export_assets_recovery.json](../evaluations/cases/figma_export_assets_recovery.json) | Recovery evaluation specification for figma.export_assets. |
| A | [evaluations/cases/figma_export_assets_taglish.json](../evaluations/cases/figma_export_assets_taglish.json) | Taglish evaluation specification for figma.export_assets. |
| A | [evaluations/cases/figma_frames_constraints_english.json](../evaluations/cases/figma_frames_constraints_english.json) | English evaluation specification for figma.frames_constraints. |
| A | [evaluations/cases/figma_frames_constraints_recovery.json](../evaluations/cases/figma_frames_constraints_recovery.json) | Recovery evaluation specification for figma.frames_constraints. |
| A | [evaluations/cases/figma_frames_constraints_taglish.json](../evaluations/cases/figma_frames_constraints_taglish.json) | Taglish evaluation specification for figma.frames_constraints. |
| A | [evaluations/cases/figma_interactive_states_english.json](../evaluations/cases/figma_interactive_states_english.json) | English evaluation specification for figma.interactive_states. |
| A | [evaluations/cases/figma_interactive_states_recovery.json](../evaluations/cases/figma_interactive_states_recovery.json) | Recovery evaluation specification for figma.interactive_states. |
| A | [evaluations/cases/figma_interactive_states_taglish.json](../evaluations/cases/figma_interactive_states_taglish.json) | Taglish evaluation specification for figma.interactive_states. |
| A | [evaluations/cases/figma_mask_vector_english.json](../evaluations/cases/figma_mask_vector_english.json) | English evaluation specification for figma.mask_vector. |
| A | [evaluations/cases/figma_mask_vector_recovery.json](../evaluations/cases/figma_mask_vector_recovery.json) | Recovery evaluation specification for figma.mask_vector. |
| A | [evaluations/cases/figma_mask_vector_taglish.json](../evaluations/cases/figma_mask_vector_taglish.json) | Taglish evaluation specification for figma.mask_vector. |
| A | [evaluations/cases/figma_nested_auto_layout_english.json](../evaluations/cases/figma_nested_auto_layout_english.json) | English evaluation specification for figma.nested_auto_layout. |
| A | [evaluations/cases/figma_nested_auto_layout_recovery.json](../evaluations/cases/figma_nested_auto_layout_recovery.json) | Recovery evaluation specification for figma.nested_auto_layout. |
| A | [evaluations/cases/figma_nested_auto_layout_taglish.json](../evaluations/cases/figma_nested_auto_layout_taglish.json) | Taglish evaluation specification for figma.nested_auto_layout. |
| A | [evaluations/cases/figma_prototype_navigation_english.json](../evaluations/cases/figma_prototype_navigation_english.json) | English evaluation specification for figma.prototype_navigation. |
| A | [evaluations/cases/figma_prototype_navigation_recovery.json](../evaluations/cases/figma_prototype_navigation_recovery.json) | Recovery evaluation specification for figma.prototype_navigation. |
| A | [evaluations/cases/figma_prototype_navigation_taglish.json](../evaluations/cases/figma_prototype_navigation_taglish.json) | Taglish evaluation specification for figma.prototype_navigation. |
| A | [evaluations/cases/figma_variables_modes_english.json](../evaluations/cases/figma_variables_modes_english.json) | English evaluation specification for figma.variables_modes. |
| A | [evaluations/cases/figma_variables_modes_recovery.json](../evaluations/cases/figma_variables_modes_recovery.json) | Recovery evaluation specification for figma.variables_modes. |
| A | [evaluations/cases/figma_variables_modes_taglish.json](../evaluations/cases/figma_variables_modes_taglish.json) | Taglish evaluation specification for figma.variables_modes. |
| A | [evaluations/cases/finder_archives_english.json](../evaluations/cases/finder_archives_english.json) | English evaluation specification for finder.archives. |
| A | [evaluations/cases/finder_archives_recovery.json](../evaluations/cases/finder_archives_recovery.json) | Recovery evaluation specification for finder.archives. |
| A | [evaluations/cases/finder_archives_taglish.json](../evaluations/cases/finder_archives_taglish.json) | Taglish evaluation specification for finder.archives. |
| A | [evaluations/cases/finder_create_folder.json](../evaluations/cases/finder_create_folder.json) | English evaluation specification for finder.create_folder. |
| A | [evaluations/cases/finder_create_folder_english.json](../evaluations/cases/finder_create_folder_english.json) | English evaluation specification for finder.create_folder. |
| A | [evaluations/cases/finder_create_folder_recovery.json](../evaluations/cases/finder_create_folder_recovery.json) | Recovery evaluation specification for finder.create_folder. |
| A | [evaluations/cases/finder_create_folder_taglish.json](../evaluations/cases/finder_create_folder_taglish.json) | Taglish evaluation specification for finder.create_folder. |
| A | [evaluations/cases/finder_downloads_english.json](../evaluations/cases/finder_downloads_english.json) | English evaluation specification for finder.downloads. |
| A | [evaluations/cases/finder_downloads_recovery.json](../evaluations/cases/finder_downloads_recovery.json) | Recovery evaluation specification for finder.downloads. |
| A | [evaluations/cases/finder_downloads_taglish.json](../evaluations/cases/finder_downloads_taglish.json) | Taglish evaluation specification for finder.downloads. |
| A | [evaluations/cases/finder_file_info_english.json](../evaluations/cases/finder_file_info_english.json) | English evaluation specification for finder.file_info. |
| A | [evaluations/cases/finder_file_info_recovery.json](../evaluations/cases/finder_file_info_recovery.json) | Recovery evaluation specification for finder.file_info. |
| A | [evaluations/cases/finder_file_info_taglish.json](../evaluations/cases/finder_file_info_taglish.json) | Taglish evaluation specification for finder.file_info. |
| A | [evaluations/cases/finder_organize_english.json](../evaluations/cases/finder_organize_english.json) | English evaluation specification for finder.organize. |
| A | [evaluations/cases/finder_organize_recovery.json](../evaluations/cases/finder_organize_recovery.json) | Recovery evaluation specification for finder.organize. |
| A | [evaluations/cases/finder_organize_taglish.json](../evaluations/cases/finder_organize_taglish.json) | Taglish evaluation specification for finder.organize. |
| A | [evaluations/cases/finder_rename_english.json](../evaluations/cases/finder_rename_english.json) | English evaluation specification for finder.rename. |
| A | [evaluations/cases/finder_rename_recovery.json](../evaluations/cases/finder_rename_recovery.json) | Recovery evaluation specification for finder.rename. |
| A | [evaluations/cases/finder_rename_taglish.json](../evaluations/cases/finder_rename_taglish.json) | Taglish evaluation specification for finder.rename. |
| A | [evaluations/cases/finder_tags_search_english.json](../evaluations/cases/finder_tags_search_english.json) | English evaluation specification for finder.tags_search. |
| A | [evaluations/cases/finder_tags_search_recovery.json](../evaluations/cases/finder_tags_search_recovery.json) | Recovery evaluation specification for finder.tags_search. |
| A | [evaluations/cases/finder_tags_search_taglish.json](../evaluations/cases/finder_tags_search_taglish.json) | Taglish evaluation specification for finder.tags_search. |
| A | [evaluations/cases/fl_studio_audio_setup_english.json](../evaluations/cases/fl_studio_audio_setup_english.json) | English evaluation specification for fl_studio.audio_setup. |
| A | [evaluations/cases/fl_studio_audio_setup_recovery.json](../evaluations/cases/fl_studio_audio_setup_recovery.json) | Recovery evaluation specification for fl_studio.audio_setup. |
| A | [evaluations/cases/fl_studio_audio_setup_taglish.json](../evaluations/cases/fl_studio_audio_setup_taglish.json) | Taglish evaluation specification for fl_studio.audio_setup. |
| A | [evaluations/cases/fl_studio_automation_english.json](../evaluations/cases/fl_studio_automation_english.json) | English evaluation specification for fl_studio.automation. |
| A | [evaluations/cases/fl_studio_automation_recovery.json](../evaluations/cases/fl_studio_automation_recovery.json) | Recovery evaluation specification for fl_studio.automation. |
| A | [evaluations/cases/fl_studio_automation_taglish.json](../evaluations/cases/fl_studio_automation_taglish.json) | Taglish evaluation specification for fl_studio.automation. |
| A | [evaluations/cases/fl_studio_drum_pattern_english.json](../evaluations/cases/fl_studio_drum_pattern_english.json) | English evaluation specification for fl_studio.drum_pattern. |
| A | [evaluations/cases/fl_studio_drum_pattern_recovery.json](../evaluations/cases/fl_studio_drum_pattern_recovery.json) | Recovery evaluation specification for fl_studio.drum_pattern. |
| A | [evaluations/cases/fl_studio_drum_pattern_taglish.json](../evaluations/cases/fl_studio_drum_pattern_taglish.json) | Taglish evaluation specification for fl_studio.drum_pattern. |
| A | [evaluations/cases/fl_studio_export_stems_english.json](../evaluations/cases/fl_studio_export_stems_english.json) | English evaluation specification for fl_studio.export_stems. |
| A | [evaluations/cases/fl_studio_export_stems_recovery.json](../evaluations/cases/fl_studio_export_stems_recovery.json) | Recovery evaluation specification for fl_studio.export_stems. |
| A | [evaluations/cases/fl_studio_export_stems_taglish.json](../evaluations/cases/fl_studio_export_stems_taglish.json) | Taglish evaluation specification for fl_studio.export_stems. |
| A | [evaluations/cases/fl_studio_piano_roll_english.json](../evaluations/cases/fl_studio_piano_roll_english.json) | English evaluation specification for fl_studio.piano_roll. |
| A | [evaluations/cases/fl_studio_piano_roll_recovery.json](../evaluations/cases/fl_studio_piano_roll_recovery.json) | Recovery evaluation specification for fl_studio.piano_roll. |
| A | [evaluations/cases/fl_studio_piano_roll_taglish.json](../evaluations/cases/fl_studio_piano_roll_taglish.json) | Taglish evaluation specification for fl_studio.piano_roll. |
| A | [evaluations/cases/fl_studio_record_takes_english.json](../evaluations/cases/fl_studio_record_takes_english.json) | English evaluation specification for fl_studio.record_takes. |
| A | [evaluations/cases/fl_studio_record_takes_recovery.json](../evaluations/cases/fl_studio_record_takes_recovery.json) | Recovery evaluation specification for fl_studio.record_takes. |
| A | [evaluations/cases/fl_studio_record_takes_taglish.json](../evaluations/cases/fl_studio_record_takes_taglish.json) | Taglish evaluation specification for fl_studio.record_takes. |
| A | [evaluations/cases/fl_studio_sidechain_english.json](../evaluations/cases/fl_studio_sidechain_english.json) | English evaluation specification for fl_studio.sidechain. |
| A | [evaluations/cases/fl_studio_sidechain_recovery.json](../evaluations/cases/fl_studio_sidechain_recovery.json) | Recovery evaluation specification for fl_studio.sidechain. |
| A | [evaluations/cases/fl_studio_sidechain_taglish.json](../evaluations/cases/fl_studio_sidechain_taglish.json) | Taglish evaluation specification for fl_studio.sidechain. |
| A | [evaluations/cases/fl_studio_unique_arrangement_english.json](../evaluations/cases/fl_studio_unique_arrangement_english.json) | English evaluation specification for fl_studio.unique_arrangement. |
| A | [evaluations/cases/fl_studio_unique_arrangement_recovery.json](../evaluations/cases/fl_studio_unique_arrangement_recovery.json) | Recovery evaluation specification for fl_studio.unique_arrangement. |
| A | [evaluations/cases/fl_studio_unique_arrangement_taglish.json](../evaluations/cases/fl_studio_unique_arrangement_taglish.json) | Taglish evaluation specification for fl_studio.unique_arrangement. |
| A | [evaluations/cases/illustrator_artboards_english.json](../evaluations/cases/illustrator_artboards_english.json) | English evaluation specification for illustrator.artboards. |
| A | [evaluations/cases/illustrator_artboards_recovery.json](../evaluations/cases/illustrator_artboards_recovery.json) | Recovery evaluation specification for illustrator.artboards. |
| A | [evaluations/cases/illustrator_artboards_taglish.json](../evaluations/cases/illustrator_artboards_taglish.json) | Taglish evaluation specification for illustrator.artboards. |
| A | [evaluations/cases/illustrator_clipping_mask_english.json](../evaluations/cases/illustrator_clipping_mask_english.json) | English evaluation specification for illustrator.clipping_mask. |
| A | [evaluations/cases/illustrator_clipping_mask_recovery.json](../evaluations/cases/illustrator_clipping_mask_recovery.json) | Recovery evaluation specification for illustrator.clipping_mask. |
| A | [evaluations/cases/illustrator_clipping_mask_taglish.json](../evaluations/cases/illustrator_clipping_mask_taglish.json) | Taglish evaluation specification for illustrator.clipping_mask. |
| A | [evaluations/cases/illustrator_export_english.json](../evaluations/cases/illustrator_export_english.json) | English evaluation specification for illustrator.export. |
| A | [evaluations/cases/illustrator_export_recovery.json](../evaluations/cases/illustrator_export_recovery.json) | Recovery evaluation specification for illustrator.export. |
| A | [evaluations/cases/illustrator_export_taglish.json](../evaluations/cases/illustrator_export_taglish.json) | Taglish evaluation specification for illustrator.export. |
| A | [evaluations/cases/illustrator_global_colors_english.json](../evaluations/cases/illustrator_global_colors_english.json) | English evaluation specification for illustrator.global_colors. |
| A | [evaluations/cases/illustrator_global_colors_recovery.json](../evaluations/cases/illustrator_global_colors_recovery.json) | Recovery evaluation specification for illustrator.global_colors. |
| A | [evaluations/cases/illustrator_global_colors_taglish.json](../evaluations/cases/illustrator_global_colors_taglish.json) | Taglish evaluation specification for illustrator.global_colors. |
| A | [evaluations/cases/illustrator_image_trace_english.json](../evaluations/cases/illustrator_image_trace_english.json) | English evaluation specification for illustrator.image_trace. |
| A | [evaluations/cases/illustrator_image_trace_recovery.json](../evaluations/cases/illustrator_image_trace_recovery.json) | Recovery evaluation specification for illustrator.image_trace. |
| A | [evaluations/cases/illustrator_image_trace_taglish.json](../evaluations/cases/illustrator_image_trace_taglish.json) | Taglish evaluation specification for illustrator.image_trace. |
| A | [evaluations/cases/illustrator_pen_paths_english.json](../evaluations/cases/illustrator_pen_paths_english.json) | English evaluation specification for illustrator.pen_paths. |
| A | [evaluations/cases/illustrator_pen_paths_recovery.json](../evaluations/cases/illustrator_pen_paths_recovery.json) | Recovery evaluation specification for illustrator.pen_paths. |
| A | [evaluations/cases/illustrator_pen_paths_taglish.json](../evaluations/cases/illustrator_pen_paths_taglish.json) | Taglish evaluation specification for illustrator.pen_paths. |
| A | [evaluations/cases/illustrator_shape_builder_english.json](../evaluations/cases/illustrator_shape_builder_english.json) | English evaluation specification for illustrator.shape_builder. |
| A | [evaluations/cases/illustrator_shape_builder_recovery.json](../evaluations/cases/illustrator_shape_builder_recovery.json) | Recovery evaluation specification for illustrator.shape_builder. |
| A | [evaluations/cases/illustrator_shape_builder_taglish.json](../evaluations/cases/illustrator_shape_builder_taglish.json) | Taglish evaluation specification for illustrator.shape_builder. |
| A | [evaluations/cases/illustrator_type_on_path_english.json](../evaluations/cases/illustrator_type_on_path_english.json) | English evaluation specification for illustrator.type_on_path. |
| A | [evaluations/cases/illustrator_type_on_path_recovery.json](../evaluations/cases/illustrator_type_on_path_recovery.json) | Recovery evaluation specification for illustrator.type_on_path. |
| A | [evaluations/cases/illustrator_type_on_path_taglish.json](../evaluations/cases/illustrator_type_on_path_taglish.json) | Taglish evaluation specification for illustrator.type_on_path. |
| A | [evaluations/cases/photoshop_adjustment_layer_english.json](../evaluations/cases/photoshop_adjustment_layer_english.json) | English evaluation specification for photoshop.adjustment_layer. |
| A | [evaluations/cases/photoshop_adjustment_layer_recovery.json](../evaluations/cases/photoshop_adjustment_layer_recovery.json) | Recovery evaluation specification for photoshop.adjustment_layer. |
| A | [evaluations/cases/photoshop_adjustment_layer_taglish.json](../evaluations/cases/photoshop_adjustment_layer_taglish.json) | Taglish evaluation specification for photoshop.adjustment_layer. |
| A | [evaluations/cases/photoshop_clipping_mask_english.json](../evaluations/cases/photoshop_clipping_mask_english.json) | English evaluation specification for photoshop.clipping_mask. |
| A | [evaluations/cases/photoshop_clipping_mask_recovery.json](../evaluations/cases/photoshop_clipping_mask_recovery.json) | Recovery evaluation specification for photoshop.clipping_mask. |
| A | [evaluations/cases/photoshop_clipping_mask_taglish.json](../evaluations/cases/photoshop_clipping_mask_taglish.json) | Taglish evaluation specification for photoshop.clipping_mask. |
| A | [evaluations/cases/photoshop_export_english.json](../evaluations/cases/photoshop_export_english.json) | English evaluation specification for photoshop.export. |
| A | [evaluations/cases/photoshop_export_recovery.json](../evaluations/cases/photoshop_export_recovery.json) | Recovery evaluation specification for photoshop.export. |
| A | [evaluations/cases/photoshop_export_taglish.json](../evaluations/cases/photoshop_export_taglish.json) | Taglish evaluation specification for photoshop.export. |
| A | [evaluations/cases/photoshop_layer_mask_english.json](../evaluations/cases/photoshop_layer_mask_english.json) | English evaluation specification for photoshop.layer_mask. |
| A | [evaluations/cases/photoshop_layer_mask_recovery.json](../evaluations/cases/photoshop_layer_mask_recovery.json) | Recovery evaluation specification for photoshop.layer_mask. |
| A | [evaluations/cases/photoshop_layer_mask_taglish.json](../evaluations/cases/photoshop_layer_mask_taglish.json) | Taglish evaluation specification for photoshop.layer_mask. |
| A | [evaluations/cases/photoshop_refine_edges_english.json](../evaluations/cases/photoshop_refine_edges_english.json) | English evaluation specification for photoshop.refine_edges. |
| A | [evaluations/cases/photoshop_refine_edges_recovery.json](../evaluations/cases/photoshop_refine_edges_recovery.json) | Recovery evaluation specification for photoshop.refine_edges. |
| A | [evaluations/cases/photoshop_refine_edges_taglish.json](../evaluations/cases/photoshop_refine_edges_taglish.json) | Taglish evaluation specification for photoshop.refine_edges. |
| A | [evaluations/cases/photoshop_separate_layer_retouch_english.json](../evaluations/cases/photoshop_separate_layer_retouch_english.json) | English evaluation specification for photoshop.separate_layer_retouch. |
| A | [evaluations/cases/photoshop_separate_layer_retouch_recovery.json](../evaluations/cases/photoshop_separate_layer_retouch_recovery.json) | Recovery evaluation specification for photoshop.separate_layer_retouch. |
| A | [evaluations/cases/photoshop_separate_layer_retouch_taglish.json](../evaluations/cases/photoshop_separate_layer_retouch_taglish.json) | Taglish evaluation specification for photoshop.separate_layer_retouch. |
| A | [evaluations/cases/photoshop_smart_filters_english.json](../evaluations/cases/photoshop_smart_filters_english.json) | English evaluation specification for photoshop.smart_filters. |
| A | [evaluations/cases/photoshop_smart_filters_recovery.json](../evaluations/cases/photoshop_smart_filters_recovery.json) | Recovery evaluation specification for photoshop.smart_filters. |
| A | [evaluations/cases/photoshop_smart_filters_taglish.json](../evaluations/cases/photoshop_smart_filters_taglish.json) | Taglish evaluation specification for photoshop.smart_filters. |
| A | [evaluations/cases/photoshop_text_shapes_english.json](../evaluations/cases/photoshop_text_shapes_english.json) | English evaluation specification for photoshop.text_shapes. |
| A | [evaluations/cases/photoshop_text_shapes_recovery.json](../evaluations/cases/photoshop_text_shapes_recovery.json) | Recovery evaluation specification for photoshop.text_shapes. |
| A | [evaluations/cases/photoshop_text_shapes_taglish.json](../evaluations/cases/photoshop_text_shapes_taglish.json) | Taglish evaluation specification for photoshop.text_shapes. |
| A | [evaluations/cases/powerpoint_align_objects_english.json](../evaluations/cases/powerpoint_align_objects_english.json) | English evaluation specification for powerpoint.align_objects. |
| A | [evaluations/cases/powerpoint_align_objects_recovery.json](../evaluations/cases/powerpoint_align_objects_recovery.json) | Recovery evaluation specification for powerpoint.align_objects. |
| A | [evaluations/cases/powerpoint_align_objects_taglish.json](../evaluations/cases/powerpoint_align_objects_taglish.json) | Taglish evaluation specification for powerpoint.align_objects. |
| A | [evaluations/cases/powerpoint_export_pdf_english.json](../evaluations/cases/powerpoint_export_pdf_english.json) | English evaluation specification for powerpoint.export_pdf. |
| A | [evaluations/cases/powerpoint_export_pdf_recovery.json](../evaluations/cases/powerpoint_export_pdf_recovery.json) | Recovery evaluation specification for powerpoint.export_pdf. |
| A | [evaluations/cases/powerpoint_export_pdf_taglish.json](../evaluations/cases/powerpoint_export_pdf_taglish.json) | Taglish evaluation specification for powerpoint.export_pdf. |
| A | [evaluations/cases/powerpoint_insert_chart_english.json](../evaluations/cases/powerpoint_insert_chart_english.json) | English evaluation specification for powerpoint.insert_chart. |
| A | [evaluations/cases/powerpoint_insert_chart_recovery.json](../evaluations/cases/powerpoint_insert_chart_recovery.json) | Recovery evaluation specification for powerpoint.insert_chart. |
| A | [evaluations/cases/powerpoint_insert_chart_taglish.json](../evaluations/cases/powerpoint_insert_chart_taglish.json) | Taglish evaluation specification for powerpoint.insert_chart. |
| A | [evaluations/cases/powerpoint_insert_image.json](../evaluations/cases/powerpoint_insert_image.json) | Taglish evaluation specification for powerpoint.insert_image. |
| A | [evaluations/cases/powerpoint_insert_image_english.json](../evaluations/cases/powerpoint_insert_image_english.json) | English evaluation specification for powerpoint.insert_image. |
| A | [evaluations/cases/powerpoint_insert_image_recovery.json](../evaluations/cases/powerpoint_insert_image_recovery.json) | Recovery evaluation specification for powerpoint.insert_image. |
| A | [evaluations/cases/powerpoint_insert_image_taglish.json](../evaluations/cases/powerpoint_insert_image_taglish.json) | Taglish evaluation specification for powerpoint.insert_image. |
| A | [evaluations/cases/powerpoint_insert_table_english.json](../evaluations/cases/powerpoint_insert_table_english.json) | English evaluation specification for powerpoint.insert_table. |
| A | [evaluations/cases/powerpoint_insert_table_recovery.json](../evaluations/cases/powerpoint_insert_table_recovery.json) | Recovery evaluation specification for powerpoint.insert_table. |
| A | [evaluations/cases/powerpoint_insert_table_taglish.json](../evaluations/cases/powerpoint_insert_table_taglish.json) | Taglish evaluation specification for powerpoint.insert_table. |
| A | [evaluations/cases/powerpoint_slide_layout_english.json](../evaluations/cases/powerpoint_slide_layout_english.json) | English evaluation specification for powerpoint.slide_layout. |
| A | [evaluations/cases/powerpoint_slide_layout_recovery.json](../evaluations/cases/powerpoint_slide_layout_recovery.json) | Recovery evaluation specification for powerpoint.slide_layout. |
| A | [evaluations/cases/powerpoint_slide_layout_taglish.json](../evaluations/cases/powerpoint_slide_layout_taglish.json) | Taglish evaluation specification for powerpoint.slide_layout. |
| A | [evaluations/cases/powerpoint_slide_master_english.json](../evaluations/cases/powerpoint_slide_master_english.json) | English evaluation specification for powerpoint.slide_master. |
| A | [evaluations/cases/powerpoint_slide_master_recovery.json](../evaluations/cases/powerpoint_slide_master_recovery.json) | Recovery evaluation specification for powerpoint.slide_master. |
| A | [evaluations/cases/powerpoint_slide_master_taglish.json](../evaluations/cases/powerpoint_slide_master_taglish.json) | Taglish evaluation specification for powerpoint.slide_master. |
| A | [evaluations/cases/powerpoint_speaker_notes_english.json](../evaluations/cases/powerpoint_speaker_notes_english.json) | English evaluation specification for powerpoint.speaker_notes. |
| A | [evaluations/cases/powerpoint_speaker_notes_recovery.json](../evaluations/cases/powerpoint_speaker_notes_recovery.json) | Recovery evaluation specification for powerpoint.speaker_notes. |
| A | [evaluations/cases/powerpoint_speaker_notes_taglish.json](../evaluations/cases/powerpoint_speaker_notes_taglish.json) | Taglish evaluation specification for powerpoint.speaker_notes. |
| A | [evaluations/cases/premiere_captions_english.json](../evaluations/cases/premiere_captions_english.json) | English evaluation specification for premiere.captions. |
| A | [evaluations/cases/premiere_captions_recovery.json](../evaluations/cases/premiere_captions_recovery.json) | Recovery evaluation specification for premiere.captions. |
| A | [evaluations/cases/premiere_captions_taglish.json](../evaluations/cases/premiere_captions_taglish.json) | Taglish evaluation specification for premiere.captions. |
| A | [evaluations/cases/premiere_color_english.json](../evaluations/cases/premiere_color_english.json) | English evaluation specification for premiere.color. |
| A | [evaluations/cases/premiere_color_recovery.json](../evaluations/cases/premiere_color_recovery.json) | Recovery evaluation specification for premiere.color. |
| A | [evaluations/cases/premiere_color_taglish.json](../evaluations/cases/premiere_color_taglish.json) | Taglish evaluation specification for premiere.color. |
| A | [evaluations/cases/premiere_dialogue_repair_english.json](../evaluations/cases/premiere_dialogue_repair_english.json) | English evaluation specification for premiere.dialogue_repair. |
| A | [evaluations/cases/premiere_dialogue_repair_recovery.json](../evaluations/cases/premiere_dialogue_repair_recovery.json) | Recovery evaluation specification for premiere.dialogue_repair. |
| A | [evaluations/cases/premiere_dialogue_repair_taglish.json](../evaluations/cases/premiere_dialogue_repair_taglish.json) | Taglish evaluation specification for premiere.dialogue_repair. |
| A | [evaluations/cases/premiere_export_english.json](../evaluations/cases/premiere_export_english.json) | English evaluation specification for premiere.export. |
| A | [evaluations/cases/premiere_export_recovery.json](../evaluations/cases/premiere_export_recovery.json) | Recovery evaluation specification for premiere.export. |
| A | [evaluations/cases/premiere_export_taglish.json](../evaluations/cases/premiere_export_taglish.json) | Taglish evaluation specification for premiere.export. |
| A | [evaluations/cases/premiere_multicam_english.json](../evaluations/cases/premiere_multicam_english.json) | English evaluation specification for premiere.multicam. |
| A | [evaluations/cases/premiere_multicam_recovery.json](../evaluations/cases/premiere_multicam_recovery.json) | Recovery evaluation specification for premiere.multicam. |
| A | [evaluations/cases/premiere_multicam_taglish.json](../evaluations/cases/premiere_multicam_taglish.json) | Taglish evaluation specification for premiere.multicam. |
| A | [evaluations/cases/premiere_proxies_english.json](../evaluations/cases/premiere_proxies_english.json) | English evaluation specification for premiere.proxies. |
| A | [evaluations/cases/premiere_proxies_recovery.json](../evaluations/cases/premiere_proxies_recovery.json) | Recovery evaluation specification for premiere.proxies. |
| A | [evaluations/cases/premiere_proxies_taglish.json](../evaluations/cases/premiere_proxies_taglish.json) | Taglish evaluation specification for premiere.proxies. |
| A | [evaluations/cases/premiere_relink_english.json](../evaluations/cases/premiere_relink_english.json) | English evaluation specification for premiere.relink. |
| A | [evaluations/cases/premiere_relink_recovery.json](../evaluations/cases/premiere_relink_recovery.json) | Recovery evaluation specification for premiere.relink. |
| A | [evaluations/cases/premiere_relink_taglish.json](../evaluations/cases/premiere_relink_taglish.json) | Taglish evaluation specification for premiere.relink. |
| A | [evaluations/cases/premiere_timeline_edit_english.json](../evaluations/cases/premiere_timeline_edit_english.json) | English evaluation specification for premiere.timeline_edit. |
| A | [evaluations/cases/premiere_timeline_edit_recovery.json](../evaluations/cases/premiere_timeline_edit_recovery.json) | Recovery evaluation specification for premiere.timeline_edit. |
| A | [evaluations/cases/premiere_timeline_edit_taglish.json](../evaluations/cases/premiere_timeline_edit_taglish.json) | Taglish evaluation specification for premiere.timeline_edit. |
| A | [evaluations/cases/preview_annotate_english.json](../evaluations/cases/preview_annotate_english.json) | English evaluation specification for preview.annotate. |
| A | [evaluations/cases/preview_annotate_recovery.json](../evaluations/cases/preview_annotate_recovery.json) | Recovery evaluation specification for preview.annotate. |
| A | [evaluations/cases/preview_annotate_taglish.json](../evaluations/cases/preview_annotate_taglish.json) | Taglish evaluation specification for preview.annotate. |
| A | [evaluations/cases/preview_compress_pdf_english.json](../evaluations/cases/preview_compress_pdf_english.json) | English evaluation specification for preview.compress_pdf. |
| A | [evaluations/cases/preview_compress_pdf_recovery.json](../evaluations/cases/preview_compress_pdf_recovery.json) | Recovery evaluation specification for preview.compress_pdf. |
| A | [evaluations/cases/preview_compress_pdf_taglish.json](../evaluations/cases/preview_compress_pdf_taglish.json) | Taglish evaluation specification for preview.compress_pdf. |
| A | [evaluations/cases/preview_convert_image_english.json](../evaluations/cases/preview_convert_image_english.json) | English evaluation specification for preview.convert_image. |
| A | [evaluations/cases/preview_convert_image_recovery.json](../evaluations/cases/preview_convert_image_recovery.json) | Recovery evaluation specification for preview.convert_image. |
| A | [evaluations/cases/preview_convert_image_taglish.json](../evaluations/cases/preview_convert_image_taglish.json) | Taglish evaluation specification for preview.convert_image. |
| A | [evaluations/cases/preview_merge_pdfs.json](../evaluations/cases/preview_merge_pdfs.json) | Taglish evaluation specification for preview.merge_pdfs. |
| A | [evaluations/cases/preview_merge_pdfs_english.json](../evaluations/cases/preview_merge_pdfs_english.json) | English evaluation specification for preview.merge_pdfs. |
| A | [evaluations/cases/preview_merge_pdfs_recovery.json](../evaluations/cases/preview_merge_pdfs_recovery.json) | Recovery evaluation specification for preview.merge_pdfs. |
| A | [evaluations/cases/preview_merge_pdfs_taglish.json](../evaluations/cases/preview_merge_pdfs_taglish.json) | Taglish evaluation specification for preview.merge_pdfs. |
| A | [evaluations/cases/safari_download_destination_english.json](../evaluations/cases/safari_download_destination_english.json) | English evaluation specification for safari.download_destination. |
| A | [evaluations/cases/safari_download_destination_recovery.json](../evaluations/cases/safari_download_destination_recovery.json) | Recovery evaluation specification for safari.download_destination. |
| A | [evaluations/cases/safari_download_destination_taglish.json](../evaluations/cases/safari_download_destination_taglish.json) | Taglish evaluation specification for safari.download_destination. |
| A | [evaluations/cases/safari_download_english.json](../evaluations/cases/safari_download_english.json) | English evaluation specification for safari.download. |
| A | [evaluations/cases/safari_download_recovery.json](../evaluations/cases/safari_download_recovery.json) | Recovery evaluation specification for safari.download. |
| A | [evaluations/cases/safari_download_taglish.json](../evaluations/cases/safari_download_taglish.json) | Taglish evaluation specification for safari.download. |
| A | [evaluations/cases/safari_locate_download_english.json](../evaluations/cases/safari_locate_download_english.json) | English evaluation specification for safari.locate_download. |
| A | [evaluations/cases/safari_locate_download_recovery.json](../evaluations/cases/safari_locate_download_recovery.json) | Recovery evaluation specification for safari.locate_download. |
| A | [evaluations/cases/safari_locate_download_taglish.json](../evaluations/cases/safari_locate_download_taglish.json) | Taglish evaluation specification for safari.locate_download. |
| A | [evaluations/cases/safari_save_pdf_english.json](../evaluations/cases/safari_save_pdf_english.json) | English evaluation specification for safari.save_pdf. |
| A | [evaluations/cases/safari_save_pdf_recovery.json](../evaluations/cases/safari_save_pdf_recovery.json) | Recovery evaluation specification for safari.save_pdf. |
| A | [evaluations/cases/safari_save_pdf_taglish.json](../evaluations/cases/safari_save_pdf_taglish.json) | Taglish evaluation specification for safari.save_pdf. |
| A | [evaluations/cases/terminal_copy_move_english.json](../evaluations/cases/terminal_copy_move_english.json) | English evaluation specification for terminal.copy_move. |
| A | [evaluations/cases/terminal_copy_move_recovery.json](../evaluations/cases/terminal_copy_move_recovery.json) | Recovery evaluation specification for terminal.copy_move. |
| A | [evaluations/cases/terminal_copy_move_taglish.json](../evaluations/cases/terminal_copy_move_taglish.json) | Taglish evaluation specification for terminal.copy_move. |
| A | [evaluations/cases/terminal_create_folder_english.json](../evaluations/cases/terminal_create_folder_english.json) | English evaluation specification for terminal.create_folder. |
| A | [evaluations/cases/terminal_create_folder_recovery.json](../evaluations/cases/terminal_create_folder_recovery.json) | Recovery evaluation specification for terminal.create_folder. |
| A | [evaluations/cases/terminal_create_folder_taglish.json](../evaluations/cases/terminal_create_folder_taglish.json) | Taglish evaluation specification for terminal.create_folder. |
| A | [evaluations/cases/terminal_find_files_english.json](../evaluations/cases/terminal_find_files_english.json) | English evaluation specification for terminal.find_files. |
| A | [evaluations/cases/terminal_find_files_recovery.json](../evaluations/cases/terminal_find_files_recovery.json) | Recovery evaluation specification for terminal.find_files. |
| A | [evaluations/cases/terminal_find_files_taglish.json](../evaluations/cases/terminal_find_files_taglish.json) | Taglish evaluation specification for terminal.find_files. |
| A | [evaluations/cases/terminal_inspect_files_english.json](../evaluations/cases/terminal_inspect_files_english.json) | English evaluation specification for terminal.inspect_files. |
| A | [evaluations/cases/terminal_inspect_files_recovery.json](../evaluations/cases/terminal_inspect_files_recovery.json) | Recovery evaluation specification for terminal.inspect_files. |
| A | [evaluations/cases/terminal_inspect_files_taglish.json](../evaluations/cases/terminal_inspect_files_taglish.json) | Taglish evaluation specification for terminal.inspect_files. |
| A | [evaluations/cases/terminal_navigate_english.json](../evaluations/cases/terminal_navigate_english.json) | English evaluation specification for terminal.navigate. |
| A | [evaluations/cases/terminal_navigate_recovery.json](../evaluations/cases/terminal_navigate_recovery.json) | Recovery evaluation specification for terminal.navigate. |
| A | [evaluations/cases/terminal_navigate_taglish.json](../evaluations/cases/terminal_navigate_taglish.json) | Taglish evaluation specification for terminal.navigate. |
| A | [evaluations/cases/terminal_understand_stop_english.json](../evaluations/cases/terminal_understand_stop_english.json) | English evaluation specification for terminal.understand_stop. |
| A | [evaluations/cases/terminal_understand_stop_recovery.json](../evaluations/cases/terminal_understand_stop_recovery.json) | Recovery evaluation specification for terminal.understand_stop. |
| A | [evaluations/cases/terminal_understand_stop_taglish.json](../evaluations/cases/terminal_understand_stop_taglish.json) | Taglish evaluation specification for terminal.understand_stop. |
| A | [evaluations/cases/word_citations_english.json](../evaluations/cases/word_citations_english.json) | English evaluation specification for word.citations. |
| A | [evaluations/cases/word_citations_recovery.json](../evaluations/cases/word_citations_recovery.json) | Recovery evaluation specification for word.citations. |
| A | [evaluations/cases/word_citations_taglish.json](../evaluations/cases/word_citations_taglish.json) | Taglish evaluation specification for word.citations. |
| A | [evaluations/cases/word_headings_english.json](../evaluations/cases/word_headings_english.json) | English evaluation specification for word.headings. |
| A | [evaluations/cases/word_headings_recovery.json](../evaluations/cases/word_headings_recovery.json) | Recovery evaluation specification for word.headings. |
| A | [evaluations/cases/word_headings_taglish.json](../evaluations/cases/word_headings_taglish.json) | Taglish evaluation specification for word.headings. |
| A | [evaluations/cases/word_image_wrapping_english.json](../evaluations/cases/word_image_wrapping_english.json) | English evaluation specification for word.image_wrapping. |
| A | [evaluations/cases/word_image_wrapping_recovery.json](../evaluations/cases/word_image_wrapping_recovery.json) | Recovery evaluation specification for word.image_wrapping. |
| A | [evaluations/cases/word_image_wrapping_taglish.json](../evaluations/cases/word_image_wrapping_taglish.json) | Taglish evaluation specification for word.image_wrapping. |
| A | [evaluations/cases/word_insert_table_english.json](../evaluations/cases/word_insert_table_english.json) | English evaluation specification for word.insert_table. |
| A | [evaluations/cases/word_insert_table_recovery.json](../evaluations/cases/word_insert_table_recovery.json) | Recovery evaluation specification for word.insert_table. |
| A | [evaluations/cases/word_insert_table_taglish.json](../evaluations/cases/word_insert_table_taglish.json) | Taglish evaluation specification for word.insert_table. |
| A | [evaluations/cases/word_page_numbers_english.json](../evaluations/cases/word_page_numbers_english.json) | English evaluation specification for word.page_numbers. |
| A | [evaluations/cases/word_page_numbers_recovery.json](../evaluations/cases/word_page_numbers_recovery.json) | Recovery evaluation specification for word.page_numbers. |
| A | [evaluations/cases/word_page_numbers_taglish.json](../evaluations/cases/word_page_numbers_taglish.json) | Taglish evaluation specification for word.page_numbers. |
| A | [evaluations/cases/word_section_break_english.json](../evaluations/cases/word_section_break_english.json) | English evaluation specification for word.section_break. |
| A | [evaluations/cases/word_section_break_recovery.json](../evaluations/cases/word_section_break_recovery.json) | Recovery evaluation specification for word.section_break. |
| A | [evaluations/cases/word_section_break_taglish.json](../evaluations/cases/word_section_break_taglish.json) | Taglish evaluation specification for word.section_break. |
| A | [evaluations/cases/word_table_of_contents_english.json](../evaluations/cases/word_table_of_contents_english.json) | English evaluation specification for word.table_of_contents. |
| A | [evaluations/cases/word_table_of_contents_recovery.json](../evaluations/cases/word_table_of_contents_recovery.json) | Recovery evaluation specification for word.table_of_contents. |
| A | [evaluations/cases/word_table_of_contents_taglish.json](../evaluations/cases/word_table_of_contents_taglish.json) | Taglish evaluation specification for word.table_of_contents. |
| A | [evaluations/cases/word_tracked_changes_english.json](../evaluations/cases/word_tracked_changes_english.json) | English evaluation specification for word.tracked_changes. |
| A | [evaluations/cases/word_tracked_changes_recovery.json](../evaluations/cases/word_tracked_changes_recovery.json) | Recovery evaluation specification for word.tracked_changes. |
| A | [evaluations/cases/word_tracked_changes_taglish.json](../evaluations/cases/word_tracked_changes_taglish.json) | Taglish evaluation specification for word.tracked_changes. |
| A | [evaluations/fixtures/setups.json](../evaluations/fixtures/setups.json) | Concrete synthetic practice setups for every application area. |
| A | [evaluations/run_template.json](../evaluations/run_template.json) | Empty real-run evidence and measurement record. |
| A | [evaluations/runtime_cases.json](../evaluations/runtime_cases.json) | Shared adverse observation and inference test scenarios. |
| A | [skills/after_effects/alpha_export.json](../skills/after_effects/alpha_export.json) | Task data: Render a composition with transparency. |
| A | [skills/after_effects/composition.json](../skills/after_effects/composition.json) | Task data: Set up an After Effects composition. |
| A | [skills/after_effects/graph_editor.json](../skills/after_effects/graph_editor.json) | Task data: Shape motion in the Graph Editor. |
| A | [skills/after_effects/keyframes_easing.json](../skills/after_effects/keyframes_easing.json) | Task data: Animate and ease keyframes. |
| A | [skills/after_effects/motion_tracking.json](../skills/after_effects/motion_tracking.json) | Task data: Track motion onto a null. |
| A | [skills/after_effects/parenting.json](../skills/after_effects/parenting.json) | Task data: Control layers with parenting. |
| A | [skills/after_effects/precompose.json](../skills/after_effects/precompose.json) | Task data: Precompose related layers. |
| A | [skills/after_effects/track_matte.json](../skills/after_effects/track_matte.json) | Task data: Reveal a layer with a track matte. |
| A | [skills/canva/audio_timing.json](../skills/canva/audio_timing.json) | Task data: Trim and time Canva audio. |
| A | [skills/canva/branding.json](../skills/canva/branding.json) | Task data: Apply Canva brand assets. |
| A | [skills/canva/export_images.json](../skills/canva/export_images.json) | Task data: Download Canva images. |
| A | [skills/canva/export_pdf.json](../skills/canva/export_pdf.json) | Task data: Prepare a Canva PDF for print or accessibility. |
| A | [skills/canva/frames_crop.json](../skills/canva/frames_crop.json) | Task data: Crop images inside Canva frames. |
| A | [skills/canva/layers_align.json](../skills/canva/layers_align.json) | Task data: Arrange and align Canva layers. |
| A | [skills/canva/multipage_design.json](../skills/canva/multipage_design.json) | Task data: Build a multipage Canva design. |
| A | [skills/canva/timed_titles.json](../skills/canva/timed_titles.json) | Task data: Time titles in a Canva video. |
| A | [skills/capcut/arrange_clips.json](../skills/capcut/arrange_clips.json) | Task data: Arrange clips and overlays in CapCut. |
| A | [skills/capcut/captions.json](../skills/capcut/captions.json) | Task data: Generate and correct CapCut captions. |
| A | [skills/capcut/color_correction.json](../skills/capcut/color_correction.json) | Task data: Correct color in CapCut. |
| A | [skills/capcut/export.json](../skills/capcut/export.json) | Task data: Export a CapCut video. |
| A | [skills/capcut/import_trim.json](../skills/capcut/import_trim.json) | Task data: Import and trim a CapCut clip. |
| A | [skills/capcut/keyframes.json](../skills/capcut/keyframes.json) | Task data: Animate CapCut transform keyframes. |
| A | [skills/capcut/speed_ramp.json](../skills/capcut/speed_ramp.json) | Task data: Create a CapCut speed ramp. |
| A | [skills/capcut/tracked_blur.json](../skills/capcut/tracked_blur.json) | Task data: Track a face blur in CapCut. |
| A | [skills/excel/absolute_references.json](../skills/excel/absolute_references.json) | Task data: Use absolute and mixed references. |
| A | [skills/excel/chart.json](../skills/excel/chart.json) | Task data: Create an Excel chart. |
| A | [skills/excel/clean_csv.json](../skills/excel/clean_csv.json) | Task data: Import and clean a CSV in Excel. |
| A | [skills/excel/conditional_formula.json](../skills/excel/conditional_formula.json) | Task data: Write an IF formula. |
| A | [skills/excel/dropdown.json](../skills/excel/dropdown.json) | Task data: Create a data validation dropdown. |
| A | [skills/excel/lookup.json](../skills/excel/lookup.json) | Task data: Look up a value with XLOOKUP. |
| A | [skills/excel/pivot_table.json](../skills/excel/pivot_table.json) | Task data: Summarize data with a PivotTable. |
| A | [skills/excel/totals_averages.json](../skills/excel/totals_averages.json) | Task data: Calculate totals and averages in Excel. |
| A | [skills/figma/components_variants.json](../skills/figma/components_variants.json) | Task data: Create component variants. |
| A | [skills/figma/export_assets.json](../skills/figma/export_assets.json) | Task data: Export Figma assets. |
| A | [skills/figma/frames_constraints.json](../skills/figma/frames_constraints.json) | Task data: Resize a frame using constraints. |
| A | [skills/figma/interactive_states.json](../skills/figma/interactive_states.json) | Task data: Prototype interactive button states. |
| A | [skills/figma/mask_vector.json](../skills/figma/mask_vector.json) | Task data: Mask an image with a vector shape. |
| A | [skills/figma/nested_auto_layout.json](../skills/figma/nested_auto_layout.json) | Task data: Build nested auto layout. |
| A | [skills/figma/prototype_navigation.json](../skills/figma/prototype_navigation.json) | Task data: Connect prototype navigation. |
| A | [skills/figma/variables_modes.json](../skills/figma/variables_modes.json) | Task data: Apply variables and modes. |
| A | [skills/finder/archives.json](../skills/finder/archives.json) | Task data: Create and inspect a ZIP archive. |
| A | [skills/finder/create_folder.json](../skills/finder/create_folder.json) | Task data: Create and name a folder. |
| A | [skills/finder/downloads.json](../skills/finder/downloads.json) | Task data: Find a downloaded file in Finder. |
| A | [skills/finder/file_info.json](../skills/finder/file_info.json) | Task data: Inspect file information and extensions. |
| A | [skills/finder/organize.json](../skills/finder/organize.json) | Task data: Organize Finder files into a folder. |
| A | [skills/finder/rename.json](../skills/finder/rename.json) | Task data: Rename a Finder item. |
| A | [skills/finder/tags_search.json](../skills/finder/tags_search.json) | Task data: Tag and search Finder items. |
| A | [skills/fl_studio/audio_setup.json](../skills/fl_studio/audio_setup.json) | Task data: Configure FL Studio audio on Mac. |
| A | [skills/fl_studio/automation.json](../skills/fl_studio/automation.json) | Task data: Create an automation clip. |
| A | [skills/fl_studio/drum_pattern.json](../skills/fl_studio/drum_pattern.json) | Task data: Program a drum pattern. |
| A | [skills/fl_studio/export_stems.json](../skills/fl_studio/export_stems.json) | Task data: Export a mix and Mixer stems. |
| A | [skills/fl_studio/piano_roll.json](../skills/fl_studio/piano_roll.json) | Task data: Edit notes in the Piano Roll. |
| A | [skills/fl_studio/record_takes.json](../skills/fl_studio/record_takes.json) | Task data: Record audio takes in FL Studio. |
| A | [skills/fl_studio/sidechain.json](../skills/fl_studio/sidechain.json) | Task data: Set up sidechain compression. |
| A | [skills/fl_studio/unique_arrangement.json](../skills/fl_studio/unique_arrangement.json) | Task data: Make a unique arrangement variation. |
| A | [skills/illustrator/artboards.json](../skills/illustrator/artboards.json) | Task data: Organize Illustrator artboards. |
| A | [skills/illustrator/clipping_mask.json](../skills/illustrator/clipping_mask.json) | Task data: Create an Illustrator clipping mask. |
| A | [skills/illustrator/export.json](../skills/illustrator/export.json) | Task data: Deliver SVG or PDF artwork. |
| A | [skills/illustrator/global_colors.json](../skills/illustrator/global_colors.json) | Task data: Use global color swatches. |
| A | [skills/illustrator/image_trace.json](../skills/illustrator/image_trace.json) | Task data: Trace and refine a raster image. |
| A | [skills/illustrator/pen_paths.json](../skills/illustrator/pen_paths.json) | Task data: Draw editable Pen paths. |
| A | [skills/illustrator/shape_builder.json](../skills/illustrator/shape_builder.json) | Task data: Combine shapes with Shape Builder. |
| A | [skills/illustrator/type_on_path.json](../skills/illustrator/type_on_path.json) | Task data: Place type on a path. |
| A | [skills/photoshop/adjustment_layer.json](../skills/photoshop/adjustment_layer.json) | Task data: Target an adjustment layer. |
| A | [skills/photoshop/clipping_mask.json](../skills/photoshop/clipping_mask.json) | Task data: Clip artwork to a base layer. |
| A | [skills/photoshop/export.json](../skills/photoshop/export.json) | Task data: Export a Photoshop image. |
| A | [skills/photoshop/layer_mask.json](../skills/photoshop/layer_mask.json) | Task data: Hide a background with a layer mask. |
| A | [skills/photoshop/refine_edges.json](../skills/photoshop/refine_edges.json) | Task data: Refine hair and selection edges. |
| A | [skills/photoshop/separate_layer_retouch.json](../skills/photoshop/separate_layer_retouch.json) | Task data: Retouch on a separate layer. |
| A | [skills/photoshop/smart_filters.json](../skills/photoshop/smart_filters.json) | Task data: Apply an editable Smart Filter. |
| A | [skills/photoshop/text_shapes.json](../skills/photoshop/text_shapes.json) | Task data: Create editable text and shapes. |
| A | [skills/powerpoint/align_objects.json](../skills/powerpoint/align_objects.json) | Task data: Align objects on a slide. |
| A | [skills/powerpoint/export_pdf.json](../skills/powerpoint/export_pdf.json) | Task data: Export PowerPoint slides as PDF. |
| A | [skills/powerpoint/insert_chart.json](../skills/powerpoint/insert_chart.json) | Task data: Insert a chart in PowerPoint. |
| A | [skills/powerpoint/insert_image.json](../skills/powerpoint/insert_image.json) | Task data: Insert a local image into the current slide. |
| A | [skills/powerpoint/insert_table.json](../skills/powerpoint/insert_table.json) | Task data: Insert a table in PowerPoint. |
| A | [skills/powerpoint/slide_layout.json](../skills/powerpoint/slide_layout.json) | Task data: Change a slide layout. |
| A | [skills/powerpoint/slide_master.json](../skills/powerpoint/slide_master.json) | Task data: Customize a slide master. |
| A | [skills/powerpoint/speaker_notes.json](../skills/powerpoint/speaker_notes.json) | Task data: Add speaker notes in PowerPoint. |
| A | [skills/premiere/captions.json](../skills/premiere/captions.json) | Task data: Create Premiere captions from speech. |
| A | [skills/premiere/color.json](../skills/premiere/color.json) | Task data: Correct Premiere color with Lumetri. |
| A | [skills/premiere/dialogue_repair.json](../skills/premiere/dialogue_repair.json) | Task data: Reduce dialogue noise and reverb. |
| A | [skills/premiere/export.json](../skills/premiere/export.json) | Task data: Export a Premiere sequence. |
| A | [skills/premiere/multicam.json](../skills/premiere/multicam.json) | Task data: Create and edit a multicamera sequence. |
| A | [skills/premiere/proxies.json](../skills/premiere/proxies.json) | Task data: Create and attach Premiere proxies. |
| A | [skills/premiere/relink.json](../skills/premiere/relink.json) | Task data: Relink missing Premiere media. |
| A | [skills/premiere/timeline_edit.json](../skills/premiere/timeline_edit.json) | Task data: Build a Premiere timeline edit. |
| A | [skills/preview/annotate.json](../skills/preview/annotate.json) | Task data: Annotate a PDF in Preview. |
| A | [skills/preview/compress_pdf.json](../skills/preview/compress_pdf.json) | Task data: Reduce a PDF size in Preview. |
| A | [skills/preview/convert_image.json](../skills/preview/convert_image.json) | Task data: Convert an image format in Preview. |
| A | [skills/preview/merge_pdfs.json](../skills/preview/merge_pdfs.json) | Task data: Combine PDF pages while preserving the originals. |
| A | [skills/safari/download.json](../skills/safari/download.json) | Task data: Download a file in Safari. |
| A | [skills/safari/download_destination.json](../skills/safari/download_destination.json) | Task data: Change Safari download destination. |
| A | [skills/safari/locate_download.json](../skills/safari/locate_download.json) | Task data: Locate a Safari download. |
| A | [skills/safari/save_pdf.json](../skills/safari/save_pdf.json) | Task data: Save a Safari page as PDF. |
| A | [skills/terminal/copy_move.json](../skills/terminal/copy_move.json) | Task data: Copy or move a practice file carefully. |
| A | [skills/terminal/create_folder.json](../skills/terminal/create_folder.json) | Task data: Create a practice folder in Terminal. |
| A | [skills/terminal/find_files.json](../skills/terminal/find_files.json) | Task data: Find files within a known directory. |
| A | [skills/terminal/inspect_files.json](../skills/terminal/inspect_files.json) | Task data: Inspect files from Terminal. |
| A | [skills/terminal/navigate.json](../skills/terminal/navigate.json) | Task data: Navigate folders in Terminal. |
| A | [skills/terminal/understand_stop.json](../skills/terminal/understand_stop.json) | Task data: Understand and stop a Terminal command. |
| A | [skills/word/citations.json](../skills/word/citations.json) | Task data: Insert a citation and bibliography. |
| A | [skills/word/headings.json](../skills/word/headings.json) | Task data: Structure a Word document with headings. |
| A | [skills/word/image_wrapping.json](../skills/word/image_wrapping.json) | Task data: Control image text wrapping. |
| A | [skills/word/insert_table.json](../skills/word/insert_table.json) | Task data: Insert and format a Word table. |
| A | [skills/word/page_numbers.json](../skills/word/page_numbers.json) | Task data: Set page numbers by section. |
| A | [skills/word/section_break.json](../skills/word/section_break.json) | Task data: Create an independent document section. |
| A | [skills/word/table_of_contents.json](../skills/word/table_of_contents.json) | Task data: Insert and update a table of contents. |
| A | [skills/word/tracked_changes.json](../skills/word/tracked_changes.json) | Task data: Review tracked changes in Word. |
| A | [tests/SkillLibraryTests.swift](../tests/SkillLibraryTests.swift) | Actual Swift decoding, matching, identity and progression tests. |
| A | [tests/test_application_skills.py](../tests/test_application_skills.py) | Contract regression tests using disposable seed records. |
| A | [tests/test_rich_skills.py](../tests/test_rich_skills.py) | Version 2, coverage, intent routing and bundle regression tests. |
| A | [tools/application_skills.py](../tools/application_skills.py) | Dependency-free authoring validation, matching and resource packaging. |
| A | [tools/practice_assets.py](../tools/practice_assets.py) | Creates original disposable PNG, SVG, PDF, WAV, CSV and text fixtures. |
