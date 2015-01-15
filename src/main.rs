extern crate getopts;

use std::os;

mod cli;

fn main() {
    match os::args().as_slice() {
        [_, ref cmd, args..] => cli::Cli::parse_and_run(cmd, args.as_slice()),
        _ => println!("Other?")
    }
}
