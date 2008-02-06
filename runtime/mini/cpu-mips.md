# mips cpu description file
# this file is read by genmdesc to pruduce a table with all the relevant
# information about the cpu instructions that may be used by the regsiter
# allocator, the scheduler and other parts of the arch-dependent part of mini.
#
# An opcode name is followed by a colon and optional specifiers.
# A specifier has a name, a colon and a value.
# Specifiers are separated by white space.
# Here is a description of the specifiers valid for this file and their
# possible values.
#
# dest:register       describes the destination register of an instruction
# src1:register       describes the first source register of an instruction
# src2:register       describes the second source register of an instruction
#
# register may have the following values:
#	i  integer register
#	l  integer register pair
#	v  v0 register (output from calls)
#	V  v0/v1 register pair (output from calls)
#       a  at register
#	b  base register (used in address references)
#	f  floating point register (pair - always)
#	g  floating point register return pair (f0/f1)
#
# len:number         describe the maximun length in bytes of the instruction
# number is a positive integer
#
# cost:number        describe how many cycles are needed to complete the instruction (unused)
#
# clob:spec          describe if the instruction clobbers registers or has special needs
#
# spec can be one of the following characters:
#	c  clobbers caller-save registers
#	r  'reserves' the destination register until a later instruction unreserves it
#          used mostly to set output registers in function calls
#
# flags:spec        describe if the instruction uses or sets the flags (unused)
#
# spec can be one of the following chars:
# 	s  sets the flags
#       u  uses the flags
#       m  uses and modifies the flags
#
# res:spec          describe what units are used in the processor (unused)
#
# delay:            describe delay slots (unused)
#
# the required specifiers are: len, clob (if registers are clobbered), the registers
# specifiers if the registers are actually used, flags (when scheduling is implemented).
#
# See the code in mini-x86.c for more details on how the specifiers are used.
#
memory_barrier: len:4
nop: len:4
break: len:4
jmp: len:92
call: dest:v clob:c len:20
calli: dest:v clob:c len:20
ret: len:8
br.s: len:8
brfalse.s: len:8
brtrue.s: len:8
beq.s: len:8
bge.s: len:8
bgt.s: len:8
ble.s: len:8
blt.s: len:8
bne.un.s: len:8
bge.un.s: len:8
bgt.un.s: len:8
ble.un.s: len:8
blt.un.s: len:8
br: len:16
brfalse: len:8
brtrue: len:8
beq: len:8
bge: len:8
bgt: len:8
ble: len:8
blt: len:8
bne.un: len:8
bge.un: len:8
bgt.un: len:8
ble.un: len:8
blt.un: len:8
switch: src1:i len:40
ldind.i1: dest:i len:8
ldind.u1: dest:i len:8
ldind.i2: dest:i len:8
ldind.u2: dest:i len:8
ldind.i4: dest:i len:8
ldind.u4: dest:i len:8
ldind.i: dest:i len:8
ldind.ref: dest:i len:8
stind.ref: src1:b src2:i
stind.i1: src1:b src2:i
stind.i2: src1:b src2:i
stind.i4: src1:b src2:i
stind.r4: src1:b src2:f
stind.r8: src1:b src2:f
add: dest:i src1:i src2:i len:4
sub: dest:i src1:i src2:i len:4
mul: dest:i src1:i src2:i len:20
div: dest:i src1:i src2:i len:76
div.un: dest:i src1:i src2:i len:76
rem: dest:i src1:i src2:i len:76
rem.un: dest:i src1:i src2:i len:76
and: dest:i src1:i src2:i len:4
or: dest:i src1:i src2:i len:4
xor: dest:i src1:i src2:i len:4
shl: dest:i src1:i src2:i len:4
shr: dest:i src1:i src2:i len:4
shr.un: dest:i src1:i src2:i len:4
neg: dest:i src1:i len:4
not: dest:i src1:i len:4
conv.i1: dest:i src1:i len:8
conv.i2: dest:i src1:i len:8
conv.i4: dest:i src1:i len:4
conv.r4: dest:f src1:i len:36
conv.r8: dest:f src1:i len:36
conv.u4: dest:i src1:i
callvirt: dest:v clob:c len:20
conv.r.un: dest:f src1:i len:32
throw: src1:i len:24
rethrow: src1:i len:24
ckfinite: dest:f src1:f len:24
conv.u2: dest:i src1:i len:8
conv.u1: dest:i src1:i len:4
conv.i: dest:i src1:i len:4
add.ovf: dest:i src1:i src2:i len:64
add.ovf.un: dest:i src1:i src2:i len:64
mul.ovf: dest:i src1:i src2:i len:64
# this opcode is handled specially in the code generator
mul.ovf.un: dest:i src1:i src2:i len:64
sub.ovf: dest:i src1:i src2:i len:64
sub.ovf.un: dest:i src1:i src2:i len:64
add_ovf_carry: dest:i src1:i src2:i len:64
sub_ovf_carry: dest:i src1:i src2:i len:64
add_ovf_un_carry: dest:i src1:i src2:i len:64
sub_ovf_un_carry: dest:i src1:i src2:i len:64
start_handler: len:16
endfinally: len:12
conv.u: dest:i src1:i len:4
ceq: dest:i len:16
cgt: dest:i len:16
cgt.un: dest:i len:16
clt: dest:i len:16
clt.un: dest:i len:16
localloc: dest:i src1:i len:60
rethrow: len:24
compare: src1:i src2:i len:20
compare_imm: src1:i len:20
fcompare: src1:f src2:f len:12
oparglist: src1:i len:12
outarg: src1:i len:1
outarg_imm: len:5
setret: dest:v src1:i len:4
setlret: src1:i src2:i len:12
setreg: dest:i src1:i len:8 clob:r
setregimm: dest:i len:8 clob:r
setfreg: dest:f src1:f len:8 clob:r
checkthis: src1:b len:4
voidcall: len:20 clob:c
voidcall_reg: src1:i len:20 clob:c
voidcall_membase: src1:b len:20 clob:c
fcall: dest:g len:20 clob:c
fcall_reg: dest:g src1:i len:20 clob:c
fcall_membase: dest:g src1:b len:20 clob:c
lcall: dest:V len:28 clob:c
lcall_reg: dest:V src1:i len:28 clob:c
lcall_membase: dest:V src1:b len:28 clob:c
vcall: len:16 clob:c
vcall_reg: src1:i len:20 clob:c
vcall_membase: src1:b len:20 clob:c
call_reg: dest:v src1:i len:20 clob:c
call_membase: dest:v src1:b len:20 clob:c
iconst: dest:i len:12
r4const: dest:f len:20
r8const: dest:f len:28
label: len:0
store_membase_imm: dest:b len:20
store_membase_reg: dest:b src1:i len:16
storei1_membase_imm: dest:b len:20
storei1_membase_reg: dest:b src1:i len:16
storei2_membase_imm: dest:b len:20
storei2_membase_reg: dest:b src1:i len:16
storei4_membase_imm: dest:b len:20
storei4_membase_reg: dest:b src1:i len:16
storei8_membase_imm: dest:b 
storei8_membase_reg: dest:b src1:i 
storer4_membase_reg: dest:b src1:f len:16
storer8_membase_reg: dest:b src1:f len:16
load_membase: dest:i src1:b len:16
loadi1_membase: dest:i src1:b len:16
loadu1_membase: dest:i src1:b len:16
loadi2_membase: dest:i src1:b len:16
loadu2_membase: dest:i src1:b len:16
loadi4_membase: dest:i src1:b len:16
loadu4_membase: dest:i src1:b len:16
loadi8_membase: dest:i src1:b
loadr4_membase: dest:f src1:b len:16
loadr8_membase: dest:f src1:b len:16
loadu4_mem: dest:i len:8
move: dest:i src1:i len:4
fmove: dest:f src1:f len:8
add_imm: dest:i src1:i len:12
sub_imm: dest:i src1:i len:12
mul_imm: dest:i src1:i len:20
# there is no actual support for division or reminder by immediate
# we simulate them, though (but we need to change the burg rules 
# to allocate a symbolic reg for src2)
div_imm: dest:i src1:i src2:i len:20
div_un_imm: dest:i src1:i src2:i len:12
rem_imm: dest:i src1:i src2:i len:28
rem_un_imm: dest:i src1:i src2:i len:16
and_imm: dest:i src1:i len:12
or_imm: dest:i src1:i len:12
xor_imm: dest:i src1:i len:12
shl_imm: dest:i src1:i len:8
shr_imm: dest:i src1:i len:8
shr_un_imm: dest:i src1:i len:8
cond_exc_eq: len:32
cond_exc_ne_un: len:32
cond_exc_lt: len:32
cond_exc_lt_un: len:32
cond_exc_gt: len:32
cond_exc_gt_un: len:32
cond_exc_ge: len:32
cond_exc_ge_un: len:32
cond_exc_le: len:32
cond_exc_le_un: len:32
cond_exc_ov: len:32
cond_exc_no: len:32
cond_exc_c: len:32
cond_exc_nc: len:32
long_conv_to_i1: dest:i src1:l len:32
long_conv_to_i2: dest:i src1:l len:32
long_conv_to_i4: dest:i src1:l len:32
long_conv_to_r4: dest:f src1:l len:32
long_conv_to_r8: dest:f src1:l len:32
long_conv_to_u4: dest:i src1:l len:32
long_conv_to_u8: dest:l src1:l len:32
long_conv_to_u2: dest:i src1:l len:32
long_conv_to_u1: dest:i src1:l len:32
long_conv_to_i:  dest:i src1:l len:32
long_conv_to_ovf_i: dest:i src1:i src2:i len:32
long_mul_ovf: 
long_conv_to_r_un: dest:f src1:i src2:i len:37 
float_beq:    src1:f src2:f len:16
float_bne_un: src1:f src2:f len:16
float_blt:    src1:f src2:f len:16
float_blt_un: src1:f src2:f len:16
float_bgt:    src1:f src2:f len:16
float_btg_un: src1:f src2:f len:16
float_bge:    src1:f src2:f len:16
float_bge_un: src1:f src2:f len:16
float_ble:    src1:f src2:f len:16
float_ble_un: src1:f src2:f len:16
float_add: dest:f src1:f src2:f len:4
float_sub: dest:f src1:f src2:f len:4
float_mul: dest:f src1:f src2:f len:4
float_div: dest:f src1:f src2:f len:4
float_div_un: dest:f src1:f src2:f len:4
float_rem: dest:f src1:f src2:f len:16
float_rem_un: dest:f src1:f src2:f len:16
float_neg: dest:f src1:f len:4
float_not: dest:f src1:f len:4
float_conv_to_i1: dest:i src1:f len:40
float_conv_to_i2: dest:i src1:f len:40
float_conv_to_i4: dest:i src1:f len:40
float_conv_to_i8: dest:l src1:f len:40
float_conv_to_r4: dest:f src1:f len:8
float_conv_to_u4: dest:i src1:f len:40
float_conv_to_u8: dest:l src1:f len:40
float_conv_to_u2: dest:i src1:f len:40
float_conv_to_u1: dest:i src1:f len:40
float_conv_to_i: dest:i src1:f len:40
float_ceq: dest:i src1:f src2:f len:20
float_cgt: dest:i src1:f src2:f len:20
float_cgt_un: dest:i src1:f src2:f len:20
float_clt: dest:i src1:f src2:f len:20
float_clt_un: dest:i src1:f src2:f len:20
float_conv_to_u: dest:i src1:f len:36
call_handler: len:20
endfilter: src1:i len:16
aot_const: dest:i len:8
sqrt: dest:f src1:f len:4
adc: dest:i src1:i src2:i len:4
addcc: dest:i src1:i src2:i len:4
subcc: dest:i src1:i src2:i len:4
adc_imm: dest:i src1:i len:12
addcc_imm: dest:i src1:i len:12
subcc_imm: dest:i src1:i len:12
sbb: dest:i src1:i src2:i len:4
sbb_imm: dest:i src1:i len:12
br_reg: src1:i len:8
#ppc_subfic: dest:i src1:i len:4
#ppc_subfze: dest:i src1:i len:4
bigmul: len:52 dest:l src1:i src2:i
bigmul_un: len:52 dest:l src1:i src2:i
tls_get: len:8 dest:i
mips_beq: src1:i src2:i len:24
mips_bgez: src1:i len:24
mips_bgtz: src1:i len:24
mips_blez: src1:i len:24
mips_bltz: src1:i len:24
mips_bne: src1:i src2:i len:24
mips_cvtsd: dest:f src1:f len:8
mips_fbeq: src1:f src2:f len:16
mips_fbge: src1:f src2:f len:16
mips_fbgt: src1:f src2:f len:16
mips_fble: src1:f src2:f len:16
mips_fblt: src1:f src2:f len:16
mips_fbne: src1:f src2:f len:16
mips_lwc1: dest:f src1:b len:16
mips_mtc1_s: dest:f src1:i len:8
mips_mfc1_s: dest:i src1:f len:8
mips_mtc1_d: dest:f src1:i len:8
mips_mfc1_d: dest:i src1:f len:8
mips_slti: dest:i src1:i len:4
mips_slt: dest:i src1:i src2:i len:4
mips_sltiu: dest:i src1:i len:4
mips_sltu: dest:i src1:i src2:i len:4
mips_xori: dest:i src1:i len:4
mips_cond_exc_eq: src1:i src2:i len:40
mips_cond_exc_ge: src1:i src2:i len:40
mips_cond_exc_gt: src1:i src2:i len:40
mips_cond_exc_le: src1:i src2:i len:40
mips_cond_exc_lt: src1:i src2:i len:40
mips_cond_exc_ne_un: src1:i src2:i len:40
mips_cond_exc_ge_un: src1:i src2:i len:40
mips_cond_exc_gt_un: src1:i src2:i len:40
mips_cond_exc_le_un: src1:i src2:i len:40
mips_cond_exc_lt_un: src1:i src2:i len:40
mips_cond_exc_ov: src1:i src2:i len:40
mips_cond_exc_no: src1:i src2:i len:40
mips_cond_exc_c: src1:i src2:i len:40
mips_cond_exc_nc: src1:i src2:i len:40
mips_cond_exc_ieq: src1:i src2:i len:40
mips_cond_exc_ige: src1:i src2:i len:40
mips_cond_exc_igt: src1:i src2:i len:40
mips_cond_exc_ile: src1:i src2:i len:40
mips_cond_exc_ilt: src1:i src2:i len:40
mips_cond_exc_ine_un: src1:i src2:i len:40
mips_cond_exc_ige_un: src1:i src2:i len:40
mips_cond_exc_igt_un: src1:i src2:i len:40
mips_cond_exc_ile_un: src1:i src2:i len:40
mips_cond_exc_ilt_un: src1:i src2:i len:40
mips_cond_exc_iov: src1:i src2:i len:40
mips_cond_exc_ino: src1:i src2:i len:40
mips_cond_exc_ic: src1:i src2:i len:40
mips_cond_exc_inc: src1:i src2:i len:40