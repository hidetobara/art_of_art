import os,sys,argparse,datetime
import Store


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--fire", action="store_true", help="test firebase")
    args = parser.parse_args()
    if args.fire:
        store = Store.Store()
        print(store.collect_favorites())
