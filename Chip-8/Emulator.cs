using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using static System.Net.Mime.MediaTypeNames;

namespace Chip_8
{
    internal class Emulator
    {
        // stores current opcode
        private ushort _opcode;

        // Chip-8 has 4096 bytes of memory
        //0x000-0x1FF - Chip 8 interpreter (contains font set in emu)
        //0x050-0x0A0 - Used for the built in 4x5 pixel font set(0-F)
        //0x200-0xFFF - Program ROM and work RAM
        private byte[] _memory;
        
        // CPU registers 
        private byte[] V;

        // index Register 
        private ushort indexRegister;

        // program counter
        private ushort programCounter;

        // graphics
        public List<byte> gfx;
        public bool drawFlag;


        // timer registers
        private byte _delay_timer;
        private byte _sound_timer;


        // stack and stack pointer 
        private ushort[] _stack;
        private ushort _stackPointer;


        // Random used for random number generation
        private Random _random;

        // stores pressed keys
        public byte[] key = new byte[16];

        // font
        private byte[] _font =
        {
            0xF0, 0x90, 0x90, 0x90, 0xF0, // 0
            0x20, 0x60, 0x20, 0x20, 0x70, // 1
            0xF0, 0x10, 0xF0, 0x80, 0xF0, // 2
            0xF0, 0x10, 0xF0, 0x10, 0xF0, // 3
            0x90, 0x90, 0xF0, 0x10, 0x10, // 4
            0xF0, 0x80, 0xF0, 0x10, 0xF0, // 5
            0xF0, 0x80, 0xF0, 0x90, 0xF0, // 6
            0xF0, 0x10, 0x20, 0x40, 0x40, // 7
            0xF0, 0x90, 0xF0, 0x90, 0xF0, // 8
            0xF0, 0x90, 0xF0, 0x10, 0xF0, // 9
            0xF0, 0x90, 0xF0, 0x90, 0x90, // A
            0xE0, 0x90, 0xE0, 0x90, 0xE0, // B
            0xF0, 0x80, 0x80, 0x80, 0xF0, // C
            0xE0, 0x90, 0x90, 0x90, 0xE0, // D
            0xF0, 0x80, 0xF0, 0x80, 0xF0, // E
            0xF0, 0x80, 0xF0, 0x80, 0x80  // F
        };

        public void InitializeChip8()
        {
            // Initialize registers and memory once

            programCounter = 0x200; 
            _opcode = 0;
            indexRegister = 0;
            _stackPointer = 0;

            gfx = new List<byte>(new byte[64 * 32]);
            drawFlag = true;
            _stack = new ushort[16];
            V = new byte[16];
            _memory = new byte[4096];
            _random = new Random();
            _delay_timer = 0;
            _sound_timer = 0;
            
            LoadFont();
            
        }

        public void EmulationCycle()
        {
            // Fetch Opcode
            _opcode = (ushort) (_memory[programCounter] << 8 | _memory[programCounter+1]);
            
            // Decode and process Opcode
            ProcessOpcode(_opcode);

            // Update Sound Timer
            if (_sound_timer > 0)
            {
                if (_sound_timer == 1)
                {
                    System.Media.SystemSounds.Beep.Play();
                }
                _sound_timer--;
            }
        }

        public void Tick60Hz()
        {
            if (_delay_timer > 0)
            {
                _delay_timer--;
            }
        }

        public void LoadFont()
        {
            for (int i = 0x0050; i < 0x00A0; i++)
            {
                _memory[i] = _font[i - 0x0050];
            }

        }

        public void ProcessOpcode(ushort opcode)
        {
            switch (opcode & 0xF000) // we use 0xF000 because it allows us to keep only first nibble (first "letter")
            {
                case 0x0000:
                    switch (opcode & 0x00FF)
                    {
                        case 0x00E0: //00E0 - Clears the screen.
                            for (int i = 0; i < 2048; i++)
                            {
                                gfx[i] = 0x0;
                            }

                            drawFlag = true;
                            programCounter += 2;
                            break;
                        case 0x00EE: //00EE - Returns from a subroutine.
                            _stackPointer--;
                            programCounter = _stack[_stackPointer];
                            programCounter += 2;
                            break;
                    }
                    //0NNN - Calls machine code routine (RCA 1802 for COSMAC VIP) at address NNN. Not necessary for most ROMs
                    break;
                
                case 0x1000:
                    //1NNN - Jumps to address NNN.
                    programCounter = (ushort)(opcode & 0x0FFF);
                    break;
                case 0x2000:
                    //2NNN - Calls subroutine at NNN.
                    _stack[_stackPointer] = programCounter;
                    _stackPointer++;
                    programCounter = (ushort)(opcode & 0x0FFF);

                    break;
                case 0x3000:
                    if (V[(opcode & 0x0F00) >> 8] == (opcode & 0x00FF))//3XNN - Skips the next instruction if VX equals NN (usually the next instruction is a jump to skip a code block).
                    {
                        programCounter += 4;
                    }
                    else
                    {
                        programCounter += 2;
                    }

                    break;
                case 0x4000:
                    if (V[(opcode & 0x0F00) >> 8]  != (opcode & 0x00FF)) //4XNN - Skips the next instruction if VX does not equal NN (usually the next instruction is a jump to skip a code block).
                    {
                        programCounter += 4;
                    }
                    else
                    {
                        programCounter += 2;
                    }
                    
                    break;
                case 0x5000:
                    //5XY0 - Skips the next instruction if VX equals VY (usually the next instruction is a jump to skip a code block).
                    if (V[(opcode & 0x0F00) >> 8] == V[(opcode & 0x00F0) >> 4])
                    {
                        programCounter += 4;
                    }
                    else
                    {
                        programCounter += 2;
                    }
                    break;
                case 0x6000:
                    V[(opcode & 0x0F00) >> 8] = (byte) (opcode & 0x00FF); //6XNN - Sets VX to NN.
                    programCounter += 2;
                    break;
                case 0x7000://7XNN - Adds NN to VX (carry flag is not changed).
                    V[(opcode & 0x0F00) >> 8] += (byte) (opcode & 0x00FF);
                    programCounter += 2;
                    break;
                case 0x8000:
                    //0x8000
                    switch (opcode & 0x000F)
                    {
                        case 0x0000://8XY0 - Sets VX to the value of VY.
                            V[(opcode & 0x0F00) >> 8] = V[(opcode & 0x00F0) >> 4];
                            programCounter += 2;
                            break;
                        case 0x0001://8XY1 - Sets VX to VX or VY. (bitwise OR operation)
                            V[(opcode & 0x0F00) >> 8] = (byte)( V[(opcode & 0x0F00) >> 8] | V[(opcode & 0x00F0) >> 4] );
                            programCounter += 2;
                            break;
                        case 0x0002://8XY2 - Sets VX to VX and VY. (bitwise AND operation)
                            V[(opcode & 0x0F00) >> 8] = (byte)( V[(opcode & 0x0F00) >> 8] & V[(opcode & 0x00F0) >> 4] );
                            programCounter += 2;
                            break;
                        case 0x0003://8XY3 - Sets VX to VX xor VY.
                            V[(opcode & 0x0F00) >> 8] = (byte)( V[(opcode & 0x0F00) >> 8] ^ V[(opcode & 0x00F0) >> 4] );
                            programCounter += 2;
                            break;
                        case 0x0004://8XY4 - Adds VY to VX. VF is set to 1 when there's a carry, and to 0 when there is not.
                            if (V[(opcode & 0x00F0) >> 4] > (0xFF - V[(opcode & 0x0F00) >> 8]))
                            {
                                V[0x000F] = 1;
                            }
                            else
                            {
                                V[0x000F] = 0; ;
                            }
                            V[(opcode & 0x0F00) >> 8] = (byte)( V[(opcode & 0x0F00) >> 8] + V[(opcode & 0x0F0) >> 4] );
                            programCounter += 2;
                            break;
                        case 0x0005://8XY5 - VY is subtracted from VX. VF is set to 0 when there's a borrow, and 1 when there is not.
                            if (V[(opcode & 0x00F0) >> 4] > (V[(opcode & 0x0F00) >> 8]))
                            {
                                V[0x000F] = 0;
                            }
                            else
                            {
                                V[0x000F] = 1; ;
                            }
                            
                            V[(opcode & 0x0F00) >> 8] = (byte)( V[(opcode & 0x0F00) >> 8] - V[(opcode & 0x0F0) >> 4] );
                            programCounter += 2;
                            break;
                        case 0x0006://8XY6 - Stores the least significant bit of VX in VF and then shifts VX to the right by 1.
                            V[0x000F] = (byte)( V[(opcode & 0x0F00) >> 8] & 0x0001 );
                            V[(opcode & 0x0F00) >> 8] = (byte)( V[(opcode & 0x0F00) >> 8] >> 1 ); // shifting to the right by 1 is same as doing division by 2
                            programCounter += 2;
                            break;
                        case 0x0007://8XY7 - Sets VX to VY minus VX. VF is set to 0 when there's a borrow, and 1 when there is not.
                            if (V[(opcode & 0x0F00) >> 8] > V[(opcode & 0x00F0) >> 4] )
                            {
                                V[0x000F] = 0;
                            }
                            else
                            {
                                V[0x000F] = 1;
                            }
                            V[(opcode & 0x0F00) >> 8] = (byte)(V[(opcode & 0x00F0) >> 4] - V[(opcode & 0x0F00) >> 8]);
                            programCounter += 2;
                            break;
                        case 0x000E://8XYE - Stores the most significant bit of VX in VF and then shifts VX to the left by 1.
                            V[0x000F] = (byte)( V[(opcode & 0x0F00) >> 8] >> 7 );
                            V[(opcode & 0x0F00) >> 8] = (byte) (V[(opcode & 0x0F00) >> 8] << 1);
                            programCounter += 2;
                            break;

                    }
                    break;
                case 0x9000: //9XY0 - Skips the next instruction if VX does not equal VY. (Usually the next instruction is a jump to skip a code block);
                    if (V[(opcode & 0x0F00) >> 8] != V[(opcode & 0x00F0) >> 4])
                    {
                        programCounter += 4;
                    }
                    else
                    {
                        programCounter += 2;
                    }
                    break;
                case 0xA000: //ANNN - Sets I to the address NNN.
                    indexRegister = (ushort) (opcode & 0x0FFF);
                    programCounter += 2;
                    break;
                case 0xB000: //BNNN - Jumps to the address NNN plus V0.
                    programCounter = (ushort)((opcode & 0x0FFF) + V[0x0000]);
                    break;

                case 0xC000: //CXNN - Sets VX to the result of a bitwise AND operation on a random number (Typically: 0 to 255) and NN.
                    V[(opcode & 0x0F00) >> 8] = (byte)(_random.Next(0, 255) & (opcode & 0x0FF));
                    programCounter += 2;
                    break;
                case 0xD000:
                    //DXYN - Draws a sprite at coordinate (VX, VY) that has a width of 8 pixels and a height of N pixels.
                    //Each row of 8 pixels is read as bit-coded starting from memory location I;
                    //I value does not change after the execution of this instruction. As described above,
                    //VF is set to 1 if any screen pixels are flipped from set to unset when the sprite is drawn, and to 0 if that does not happen.
                    byte x = (byte)(V[(opcode & 0x0F00) >> 8]);
                    byte y = (byte)(V[(opcode & 0x00F0) >> 4]);
                    byte height = (byte) (opcode & 0x000F);
                    V[0xF] = 0;
                    for (int yline = 0; yline < height; yline++)
                    {
                        
                        var pixel = _memory[indexRegister + yline];
                        for (int xline = 0; xline < 8; xline++)
                        {
                            if ((pixel & (0x80 >> xline)) != 0)
                            {
                                if (gfx[(x + xline + ((y + yline) * 64))] == 1)
                                {
                                    V[0xF] = 1;
                                }
                                gfx[x + xline + ((y + yline) * 64)] ^= 1;
                            }
                        }
                    }
                    drawFlag = true;
                    programCounter += 2;
                    break;
                case 0xE000:
                    //0xE000
                    switch (opcode & 0x00FF)
                    {
                        case 0x009E://EX9E - Skips the next instruction if the key stored in VX is pressed (usually the next instruction is a jump to skip a code block).
                            if ( key[V[(opcode & 0x0F00) >> 8]] != 0)
                            {
                                programCounter += 4;
                            }
                            else
                            {
                                programCounter += 2;
                            }
                            break;
                        case 0x00A1://EXA1 - Skips the next instruction if the key stored in VX is not pressed (usually the next instruction is a jump to skip a code block).
                            if ( key[ V[(opcode & 0x0F00) >> 8]] == 0 )
                            {
                                programCounter += 4;
                            }
                            else
                            {
                                programCounter += 2;
                            }
                            break;
                    }
                    
                    break;
                case 0xF000:
                    //0xF000
                    switch (opcode & 0x00FF)
                    {
                        case 0x0007://FX07 - Sets VX to the value of the delay timer.
                            V[(opcode & 0x0F00) >> 8] = _delay_timer;
                            programCounter += 2;
                            break;
                        case 0x000A://FX0A - A key press is awaited, and then stored in VX (blocking operation, all instruction halted until next key event).
                            bool keyPress = false;
                            for (int i = 0; i < 16; i++)
                            {
                                if (key[i] != 0)
                                {
                                    V[(opcode & 0x0F00) >> 1] = (byte) i;
                                    keyPress = true;
                                }
                            }

                            if (!keyPress)
                            {
                                return;
                            }
                            programCounter += 2;
                            break;
                        case 0x0015://FX15 - Sets the delay timer to VX.
                            _delay_timer = V[(opcode & 0x0F00) >> 8] ;
                            programCounter += 2;
                            break;
                        case 0x0018://FX18 - Sets the sound timer to VX.
                            _sound_timer = V[(opcode & 0x0F00) >> 8];
                            programCounter += 2;
                            break;
                        case 0x001E://FX1E - Adds VX to I. VF is not affected.
                            indexRegister = (ushort) (indexRegister + V[(opcode & 0x0F00) >> 8]);
                            programCounter += 2;
                            break;
                        case 0x0029://FX29 - Sets I to the location of the sprite for the character in VX.
                            //Characters 0-F (in hexadecimal) are represented by a 4x5 font.
                            indexRegister = (ushort) (V[(opcode & 0x0F00) >> 8] * 0x5);
                            programCounter += 2;
                            break;
                        case 0x0033://FX33 - Stores the binary-coded decimal representation of VX,
                            //with the hundreds digit in memory at location in I, the tens digit at location I+1, and the ones digit at location I+2.
                            _memory[indexRegister] = (byte) ( V[(opcode & 0x0F00) >> 8] / 100);
                            _memory[indexRegister + 1] = (byte)((V[(opcode & 0x0F00) >> 8] / 10) % 10);
                            _memory[indexRegister + 2] = (byte)((V[(opcode & 0x0F00) >> 8] % 100) % 10 );
                            programCounter += 2;
                            break;
                        case 0x0055://FX55 - Stores from V0 to VX (including VX) in memory, starting at address I.
                            //The offset from I is increased by 1 for each value written, but I itself is left unmodified.
                            for (int i = 0; i <= ((opcode & 0x0F00) >> 8); i++)
                            {
                                _memory[indexRegister + i] = V[i];
                            }
                            programCounter += 2;
                            break;
                        case 0x0065://FX65 - Fills from V0 to VX (including VX) with values from memory, starting at address I.
                            //The offset from I is increased by 1 for each value read, but I itself is left unmodified.
                            for (int i = 0; i <= ((opcode & 0x0F00) >> 8); i++)
                            {
                                V[i] = _memory[indexRegister + i];
                            }
                            indexRegister += (ushort) (((opcode & 0x0F00) >> 8) + 1);
                            programCounter += 2;
                            break;
                            
                        default:
                            Console.WriteLine($"Unknown opcode {opcode}");
                            break;
                    }

                    break;
                default: Console.WriteLine();
                    break;


            }


        }

        public void LoadApplication(string filename)
        {
            var buffer = File.ReadAllBytes(filename);

            if (filename.Length < (4096-512))
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    _memory[i + 512] = buffer[i];
                }
            }
        }

    }
}
