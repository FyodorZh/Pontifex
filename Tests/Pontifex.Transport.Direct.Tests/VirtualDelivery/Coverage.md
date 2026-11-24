# VirtualDelivery Coverage

| Field | Value |
| --- | --- |
| Layer | Root |
| Subject | Virtual-delivery component group |
| Directory | `Tests/Pontifex.Transport.Direct.Tests/VirtualDelivery/` |
| Inheritance parent | none |
| Contract | [`help.md`](../../../Transports/Pontifex.Transport.Direct/VirtualDelivery/Netem/help.md) |

## Covered

| Test | Contract clause or behaviour | What the test proves | Non-extractable notes |
| --- | --- | --- | --- |
| `Seeded_constructor_produces_deterministic_sequence` | `CorrelatedRandom` seeded construction | Two instances with the same rho and seed return the same 100 values | Deterministic PRNG; no time or thread dependency. |
| `Different_seeds_produce_different_sequences` | Seed independence | Two seeds diverge after a short warm-up | A collision is possible in principle; the fixed seeds are pinned. |
| `Max_rho_produces_constant_output` | Maximum correlation | `rho = uint.MaxValue` clamps every output to the first value | This is the model's saturation case, not a statistical claim. |
| `High_rho_produces_lower_variance_than_low_rho` | Correlation reduces variance | High rho has lower variance than zero rho over 1000 seeded samples | Sampling is fixed and deterministic; the assertion is an inequality, not a threshold. |
| `Reset_produces_new_sequence` | `Reset` reseeds | A reset with the same rho changes the next value | Pins that reset does not merely rewind the visible sequence. |
| `Reset_changes_rho` | `Reset` changes correlation | A constant sequence becomes non-constant after `Reset(0)` | Uses the saturation case to make the change observable. |
| `Rho_zero_output_is_statistically_uniform` | Uniform output at zero correlation | All 16 high-nibble buckets stay within ±15% of the mean over 100,000 samples | Fixed seed keeps the run deterministic. |
| `Sigma_zero_returns_mu_regardless_of_table` | `DistributionTable.Sample` with zero spread | The sample equals `mu` even when a table is supplied | Pins the early return before the table lookup. |
| `Table_sampling_uses_the_table_entries_and_stays_in_bounds` | Empirical table sampling | Samples vary with the table entries and stay inside `mu ± sigma` | The returned offset is proportional to `sigma`; the tests use a multiple of `DistScale` (8192) so the table entry is observable. |
| `Known_max_correlation_makes_output_constant` | Maximum correlation with a table | A saturated random source yields one non-`mu` sample value | `sigma` is a multiple of `DistScale`; the constant table index is seeded. |
| `Table_of_zeros_returns_mu` | Zero table entries | A table of zeros returns `mu` at a meaningful `sigma` | The table length is non-zero; the constructor rejects an empty table. |
| `Large_table_stays_in_bounds` | Table bounds | A 10,000-entry table samples inside `mu ± sigma` | Proves the lookup bound, not a statistical distribution. |
| `Constructor_with_null_table_throws` | Table construction | A null table is rejected with `ArgumentNullException` | The constructor validates the public API surface; the binding validates the `delaydistribution` struct first. |
| `Constructor_with_empty_table_throws` | Table construction | An empty table is rejected with `ArgumentException` | Prevents a later divide-by-zero in `Sample`. |
| `Probability_zero_never_drops` | `RandomLossModel` zero probability | A zero probability never drops over 1000 draws | Deterministic seeded source. |
| `Probability_max_always_drops` | Maximum probability | `uint.MaxValue` always drops | Saturation case. |
| `Approximate_loss_rate_matches_configured_probability` | Probability scaling | A 30% configuration produces 25–35% drops over 10,000 samples | Fixed seed and tolerance; this is not a statistical confidence test. |
| `FourStateLossModelTests.All_zero_transitions_never_drops` | Four-state zero transitions | A zero transition matrix never drops | One scripted draw per call; the script is fixed. |
| `All_max_transitions_produce_alternating_pattern` | Four-state saturation | All-max transitions alternate drop and pass | One scripted draw per call; no saturation flake. |
| `Reset_returns_to_initial_state_no_drop` | Four-state reset | Reset returns to the initial state | The script covers the state before and after reset. |
| `Only_p14_set_drops_when_transitioning_to_lost_burst` | Four-state `P14` | Entering lost-burst drops, then the chain advances | `P14` is isolated by zeroing the rest; the script is exact. |
| `Only_p13_set_drops_isolated_in_gap` | Four-state `P13` | Isolated-gap loss repeats while the state remains | `P13` is isolated; the lost-gap self-loop drops every call. |
| `Only_p23_set_drops_when_leaving_burst` | Four-state `P23` | The chain `TxGap → LostGap → TxBurst → LostGap` drops on entry and on burst exit | `P14` is zero so the direct lost-burst path is never entered; `P23` is the only exit from burst. |
| `FourStateLossModelTests.Mixed_transition_probabilities_select_the_expected_branches` | Four-state mixed thresholds | Non-saturated `P14`, `P13`, `P31`, `P32`, and `P23` select the expected branch at each scripted value | The script covers lost-burst, lost-gap recovery (`P31`), burst entry, a burst stay, and burst exit. |
| `GilbertElliotLossModelTests.All_zero_transitions_never_drops` | Gilbert-Elliot zero transitions | A zero matrix never drops | Two scripted draws per call; the good state is pinned. |
| `Max_p_and_k1_always_drops_and_oscillates` | Gilbert-Elliot saturation | Saturated `P` and `K1` alternate drop and pass | Two scripted draws per call; no time-seeded random source. |
| `Only_k1_set_drops_in_good_state_and_stays` | Gilbert-Elliot `K1` | Loss in the good state persists | `P` is zero, so the state never changes. |
| `Only_p_set_moves_to_bad_then_never_recovers` | Gilbert-Elliot `P` | Entering the bad state without recovery keeps dropping | `R = 0`; `H = 0` means the bad state always drops. |
| `Reset_returns_to_good_state` | Gilbert-Elliot reset | Reset restores the good state | Reset is observable on the next call. |
| `Max_h_in_bad_state_never_drops_in_bad` | Gilbert-Elliot `H` | A saturated `H` never drops in the bad state | Pins the bad-state threshold direction. |
| `GilbertElliotLossModelTests.Mixed_transition_probabilities_select_the_expected_branches` | Gilbert-Elliot mixed thresholds | Non-saturated `P`, `R`, `H`, and `K1` select the expected branch at each scripted value | The model classifies a packet with the state it entered in: a BAD→GOOD transition still applies `H` to that packet, and `K1` starts on the next call. |
| `IsEnabled_returns_false_when_rate_is_zero` | `RateLimiter.IsEnabled` | A zero rate disables pacing | Pins the sentinel value. |
| `IsEnabled_returns_true_when_rate_is_positive` | `RateLimiter.IsEnabled` | A positive rate enables pacing | Companion to the zero case. |
| `PacketTimeNs_scales_linearly_with_packet_size` | Per-packet time | Doubling the size doubles the time at a fixed rate | Asserts exact linear scaling. |
| `PacketTimeNs_is_inverse_to_rate` | Per-packet time | Doubling the rate halves the time | Exact integer relation for the chosen values. |
| `Packet_overhead_is_added_to_size` | Packet overhead | Overhead increases the computed time | Directional assertion. |
| `Cell_size_rounds_up_packet` | Cell rounding | Two sizes inside one cell produce the same time | Pins round-up, not rounding to nearest. |
| `Cell_overhead_adds_per_cell` | Cell overhead | Per-cell overhead increases the time | Directional assertion. |
| `Packet_filling_whole_cells_uses_the_exact_time` | Cell boundary | 300 bytes in 100-byte cells take 300 ns; 301 bytes take 400 ns | Pins exact whole-cell time and round-up at the next byte. |
| `Rate_one_byte_per_sec_gives_ns_per_byte` | Rate unit conversion | One byte per second yields 500,000,000,000 ns for 500 bytes | Pins the bytes-to-nanoseconds conversion. |
| `Empty_packet_has_minimal_time` | Empty packet | An empty packet takes less time than a one-byte packet | Pins the minimum packet handling. |
| `Cell_size_zero_does_not_round` | Disabled cells | Without a cell size, an adjacent one-byte increase changes the time | Proves the rounding branch is bypassed. |
| `SaturatedLossWithDuplication_DropsThePacket` | Duplication and loss ordering | A packet with both probabilities saturated is released synchronously, not delivered as the surviving duplicate | Uses the manual-pump constructor, so the assertion is independent of latency and never waits on the dequeue thread. |
| `MonotonicTime_ConvertsLargeTicksWithoutOverflow` | Overflow-free monotonic conversion | Large tick counts at `1e9` and `1e7` frequencies produce the split-arithmetic nanosecond value | Exercises tick values that overflow the old multiply-first formula. |
| `MonotonicTime_IsNonDecreasing` | Monotonic source | Two reads do not decrease | Pins the real source's monotonicity. |
| `ManualPump_DeliversALatentMessageOnlyAfterTheDelay` | Latency pipeline | A message stays queued before its due time and is delivered at it | Manual clock and pump; no real timer. |
| `ManualPump_DuplicateProbabilityDeliversEveryCopy` | Duplicate delivery | A saturated duplicate probability delivers exactly two copies | The pump runs twice. |
| `QueueLimit_ClampsDuplicationToTheAvailableRoom` | Queue limit with duplication | Duplication never exceeds `QueueLimit`; a full queue releases the next packet | Pins the clamp and the room check. |
| `ManualPump_RateLimiterPacesConsecutiveMessages` | Rate pacing | The first and second messages become due only after their byte time | Uses the manual pump and a fixed rate. |
| `ManualPump_SlotWindowClosesAfterThePacketBudget` | Slot windows | The second message waits until the next slot opens | `MaxPackets` is one. |
| `Dispose_WithQueuedMessages_ReleasesThem` | Disposal | A constructed manual system releases queued messages on dispose | The thread was never started, so dispose must not join it. |
| `ClearAndDispose_AreRepeatSafe` | Repeated release | Repeated `Clear` and `Dispose` do not throw | Pins the disposed flag. |
| `Validate_RejectsEveryNegativeField` | Configuration validation | Every negative duration, overhead, queue limit, and slot field is rejected | One assertion per field. |
| `LossModels_ValidateTheirParameterSets` | Loss-model validation | Valid parameter sets are accepted and missing or unknown ones are rejected | Covers all three models. |

## Planned

| Missing test | Owner layer |
| --- | --- |
| Uniform jitter fallback without a `DelayDistribution`: the formula lives in `NetemDeliverySystem.CalculateBaseDelay`. | Root |
| `SlotScheduler` distribution spacing and reset. | Root |
| Reorder-gap behaviour and the leaky-bucket sequence under sustained traffic. | Root |

## Deferred By Design

| Requirement | Reason |
| --- | --- |
| Wall-clock delivery through the netem background thread | The deterministic tests use the internal manual clock and pump; the real thread remains an integration facet. |
| Platform-specific timing accuracy and thread scheduling | Not observable through the deterministic component contracts. |
| Statistical confidence intervals for the loss models | The tests pin scripted transitions and bounded rates; they do not claim a statistical proof. |
